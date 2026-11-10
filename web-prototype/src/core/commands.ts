import type { CityId, FactionId, GameState, Officer, OfficerId, Policy } from './types';
import {
  addLog, chance, cityName, clamp, factionName, freeOfficers, isAdjacent, isHostile, isRuler, relation, report,
} from './state';

export interface Result { ok: boolean; msg: string }
const fail = (msg: string): Result => ({ ok: false, msg });

export type DomesticCmd = 'farm' | 'trade' | 'dike' | 'relief' | 'wall' | 'draft' | 'train';

export const COMMAND_INFO: Record<DomesticCmd, { label: string; cost: string; stat: keyof Officer['stats'] }> = {
  farm: { label: '개간', cost: '금 50', stat: 'pol' },
  trade: { label: '상업', cost: '금 50', stat: 'pol' },
  dike: { label: '치수', cost: '금 50', stat: 'pol' },
  relief: { label: '시혜', cost: '병량 1000', stat: 'cha' },
  wall: { label: '성벽', cost: '금 60', stat: 'pol' },
  draft: { label: '징병', cost: '금 약 250', stat: 'cha' },
  train: { label: '훈련', cost: '없음', stat: 'land' },
};

function readyOfficer(s: GameState, oid: OfficerId, faction: FactionId): Officer | Result {
  const o = s.officers[oid];
  if (!o || o.status !== 'serving' || o.faction !== faction) return fail('명령할 수 없는 장수다.');
  if (o.acted) return fail(`${o.name}은(는) 이번 달 이미 움직였다.`);
  return o;
}

/** 내정 명령의 예상 효과 (UI 미리보기와 실제 실행이 같은 식을 쓴다) */
export function domesticEffect(o: Officer, cmd: DomesticCmd): number {
  const st = o.stats;
  switch (cmd) {
    case 'farm':
    case 'trade': return Math.round(8 + st.pol / 6 + (o.traits.includes('내정') ? 4 : 0));
    case 'dike': return Math.round(3 + st.pol / 20);
    case 'relief': return Math.round(3 + st.cha / 20 + (o.traits.includes('인덕') ? 3 : 0));
    case 'wall': return Math.round(3 + st.pol / 25);
    case 'draft': return Math.round(1000 + st.cha * 20);
    case 'train': return Math.round(4 + st.land / 12);
  }
}

export function domestic(s: GameState, faction: FactionId, oid: OfficerId, cmd: DomesticCmd): Result {
  const r = readyOfficer(s, oid, faction);
  if ('ok' in r) return r;
  const o = r;
  const c = s.cities[o.city];
  if (c.faction !== faction) return fail('아군 도시가 아니다.');
  const eff = domesticEffect(o, cmd);
  switch (cmd) {
    case 'farm':
      if (c.gold < 50) return fail('금이 부족하다.');
      if (c.agri >= 999) return fail('더 개간할 땅이 없다.');
      c.gold -= 50; c.agri = clamp(c.agri + eff, 0, 999); break;
    case 'trade':
      if (c.gold < 50) return fail('금이 부족하다.');
      if (c.commerce >= 999) return fail('상업이 이미 최고 수준이다.');
      c.gold -= 50; c.commerce = clamp(c.commerce + eff, 0, 999); break;
    case 'dike':
      if (c.gold < 50) return fail('금이 부족하다.');
      if (c.flood >= 100) return fail('제방이 이미 완벽하다.');
      c.gold -= 50; c.flood = clamp(c.flood + eff, 0, 100); break;
    case 'relief':
      if (c.food < 1000) return fail('병량이 부족하다.');
      if (c.loyalty >= 100) return fail('민심이 이미 최고다.');
      c.food -= 1000; c.loyalty = clamp(c.loyalty + eff, 0, 100);
      s.factions[faction].mandate = clamp(s.factions[faction].mandate + 0.5, 0, 100); break;
    case 'wall':
      if (c.gold < 60) return fail('금이 부족하다.');
      if (c.wall >= 100) return fail('성벽이 이미 완벽하다.');
      c.gold -= 60; c.wall = clamp(c.wall + eff, 0, 100); break;
    case 'draft': {
      const cap = Math.floor(c.pop * 400);
      const n = Math.min(eff, cap - c.troops);
      const cost = Math.ceil(n / 10);
      if (n < 200) return fail('더 모을 장정이 없다.');
      if (c.gold < cost) return fail('금이 부족하다.');
      c.gold -= cost;
      c.training = Math.round((c.troops * c.training + n * 20) / (c.troops + n));
      c.troops += n;
      c.loyalty = clamp(c.loyalty - 3, 0, 100);
      break;
    }
    case 'train':
      if (c.training >= 100) return fail('이미 정예다.');
      if (c.troops < 100) return fail('훈련할 병사가 없다.');
      c.training = clamp(c.training + eff, 0, 100); break;
  }
  o.acted = true;
  return { ok: true, msg: `${o.name}: ${COMMAND_INFO[cmd].label} (+${eff})` };
}

/** 재야 인재 탐색 */
export function search(s: GameState, faction: FactionId, oid: OfficerId): Result {
  const r = readyOfficer(s, oid, faction);
  if ('ok' in r) return r;
  const o = r;
  o.acted = true;
  const hidden = freeOfficers(s, o.city).filter(x => !x.found);
  // 삼고초려: 제갈량은 군주가 직접 세 번 찾아가야 한다
  const zg = freeOfficers(s, o.city).find(x => x.special === 'thrice_visit');
  if (zg && isRuler(s, o)) {
    zg.found = true;
    zg.visits = (zg.visits ?? 0) + 1;
    if (zg.visits >= 3) {
      joinFaction(s, zg, faction, o.city, 90);
      report(s, `${o.name}이(가) 세 번째로 초려를 찾았다. ${zg.name}이(가) 마침내 출사했다.`);
      s.chronicle.push(`${s.year}년 ${s.month}월, ${o.name}의 삼고초려. ${zg.name} 출사.`);
      return { ok: true, msg: `${zg.name}이(가) 출사를 결심했다!` };
    }
    return { ok: true, msg: `${zg.name}의 초려를 찾았다 (${zg.visits}/3). 아직 만나주지 않는다.` };
  }
  const candidates = hidden.filter(x => x.special !== 'thrice_visit');
  if (candidates.length && chance(s, (o.stats.int + o.stats.cha) / 220)) {
    const f = candidates[0];
    f.found = true;
    return { ok: true, msg: `${cityName(s, o.city)}에서 재야의 ${f.name}을(를) 찾아냈다.` };
  }
  if (zg && !zg.found) {
    zg.found = true;
    return { ok: true, msg: `와룡이라 불리는 ${zg.name}의 소문을 들었다. 군주가 직접 찾아가야 한다.` };
  }
  const c = s.cities[o.city];
  if (chance(s, 0.3)) {
    const g = 50 + Math.floor(o.stats.int);
    c.gold += g;
    return { ok: true, msg: `인재는 없었으나 금 ${g}을(를) 얻었다.` };
  }
  return { ok: true, msg: '아무도 찾지 못했다.' };
}

export function recruitChance(s: GameState, o: Officer, target: Officer, faction: FactionId): number {
  const ruler = s.officers[s.factions[faction].ruler];
  const aff = Math.abs(ruler.affinity - target.affinity);
  const affDist = Math.min(aff, 150 - aff);
  let p = 0.15 + o.stats.cha / 220 + s.factions[faction].mandate / 400 - affDist / 120;
  if (target.status === 'captive') p -= target.loyalty / 250;
  return clamp(p, 0.05, 0.95);
}

function joinFaction(s: GameState, t: Officer, faction: FactionId, city: CityId, loyalty: number) {
  t.status = 'serving';
  t.faction = faction;
  t.city = city;
  t.loyalty = loyalty;
  t.found = true;
  t.acted = true;
  s.captives = s.captives.filter(c => c.officer !== t.id);
  s.factions[faction].mandate = clamp(s.factions[faction].mandate + 2, 0, 100);
}

export function recruit(s: GameState, faction: FactionId, oid: OfficerId, targetId: OfficerId): Result {
  const r = readyOfficer(s, oid, faction);
  if ('ok' in r) return r;
  const o = r;
  const t = s.officers[targetId];
  if (!t) return fail('대상이 없다.');
  if (t.special === 'thrice_visit' && t.status === 'free') return fail(`${t.name}은(는) 군주가 직접 세 번 찾아가야 한다 (탐색).`);
  const isFree = t.status === 'free' && t.found && t.city === o.city;
  const isCaptive = t.status === 'captive' && s.captives.some(c => c.officer === t.id && c.by === faction);
  if (!isFree && !isCaptive) return fail('이 도시에서 등용할 수 있는 인물이 아니다.');
  o.acted = true;
  const p = recruitChance(s, o, t, faction);
  if (chance(s, p)) {
    joinFaction(s, t, faction, o.city, 70 + Math.round(o.stats.cha / 10));
    report(s, `${t.name}이(가) ${factionName(s, faction)}에 몸을 맡겼다.`);
    return { ok: true, msg: `${t.name} 등용 성공!` };
  }
  return { ok: true, msg: `${t.name}이(가) 고개를 저었다. (성공 확률 ${Math.round(p * 100)}%)` };
}

export function releaseCaptive(s: GameState, faction: FactionId, targetId: OfficerId, execute: boolean): Result {
  const t = s.officers[targetId];
  const cap = s.captives.find(c => c.officer === targetId && c.by === faction);
  if (!t || !cap) return fail('포로가 아니다.');
  s.captives = s.captives.filter(c => c !== cap);
  if (execute) {
    t.status = 'dead';
    s.factions[faction].mandate = clamp(s.factions[faction].mandate - 5, 0, 100);
    for (const o of Object.values(s.officers)) if (o.faction === faction && o.status === 'serving') o.loyalty = clamp(o.loyalty - 2, 0, 100);
    report(s, `${t.name}이(가) 처형되었다. 사관은 이 일을 적어 둔다.`);
    s.chronicle.push(`${s.year}년 ${s.month}월, ${factionName(s, faction)}이(가) ${t.name}을(를) 처형했다.`);
    return { ok: true, msg: `${t.name} 처형` };
  }
  t.status = 'free';
  t.faction = null;
  t.found = true;
  s.factions[faction].mandate = clamp(s.factions[faction].mandate + 1, 0, 100);
  return { ok: true, msg: `${t.name}을(를) 풀어 주었다.` };
}

export function moveOfficer(s: GameState, faction: FactionId, oid: OfficerId, to: CityId): Result {
  const r = readyOfficer(s, oid, faction);
  if ('ok' in r) return r;
  const o = r;
  if (!isAdjacent(s, o.city, to)) return fail('인접한 도시가 아니다.');
  if (s.cities[to].faction !== faction) return fail('아군 도시로만 이동할 수 있다.');
  const from = s.cities[o.city];
  if (from.governor === o.id) from.governor = null;
  o.city = to;
  o.acted = true;
  if (!s.cities[to].governor) s.cities[to].governor = o.id;
  return { ok: true, msg: `${o.name}이(가) ${cityName(s, to)}(으)로 이동했다.` };
}

export function transport(s: GameState, faction: FactionId, oid: OfficerId, to: CityId, troops: number, gold: number, food: number): Result {
  const r = readyOfficer(s, oid, faction);
  if ('ok' in r) return r;
  const o = r;
  const from = s.cities[o.city];
  const dst = s.cities[to];
  if (!isAdjacent(s, from.id, to) || dst.faction !== faction) return fail('인접한 아군 도시로만 수송할 수 있다.');
  troops = Math.max(0, Math.min(troops, from.troops));
  gold = Math.max(0, Math.min(gold, from.gold));
  food = Math.max(0, Math.min(food, from.food));
  if (troops + gold + food <= 0) return fail('보낼 것이 없다.');
  if (troops > 0) dst.training = Math.round((dst.troops * dst.training + troops * from.training) / (dst.troops + troops));
  from.troops -= troops; dst.troops += troops;
  from.gold -= gold; dst.gold += gold;
  from.food -= food; dst.food += food;
  o.acted = true;
  return { ok: true, msg: `${cityName(s, to)}에 수송 완료` };
}

export function setPolicy(s: GameState, faction: FactionId, city: CityId, policy: Policy): Result {
  const c = s.cities[city];
  if (c.faction !== faction) return fail('아군 도시가 아니다.');
  c.policy = policy;
  return { ok: true, msg: `${c.name} 운영 방침 변경` };
}

export function allianceChance(s: GameState, o: Officer, faction: FactionId, target: FactionId): number {
  const rel = relation(s, faction, target);
  if (!rel) return 0;
  return clamp((rel.favor + o.stats.cha / 2 - 40) / 100 + (o.traits.includes('외교') ? 0.15 : 0), 0.02, 0.9);
}

export function proposeAlliance(s: GameState, faction: FactionId, oid: OfficerId, target: FactionId): Result {
  const r = readyOfficer(s, oid, faction);
  if ('ok' in r) return r;
  const o = r;
  const rel = relation(s, faction, target);
  if (!rel || !s.factions[target]?.alive) return fail('외교 상대가 없다.');
  if (rel.alliance > 0) return fail('이미 동맹 중이다.');
  const c = s.cities[o.city];
  if (c.gold < 300) return fail('예물(금 300)이 부족하다.');
  c.gold -= 300;
  o.acted = true;
  const p = allianceChance(s, o, faction, target);
  if (chance(s, p)) {
    rel.alliance = 12; rel.favor = clamp(rel.favor + 10, 0, 100);
    report(s, `${factionName(s, faction)}과(와) ${factionName(s, target)}이(가) 1년 동맹을 맺었다.`);
    return { ok: true, msg: `${factionName(s, target)}과(와) 동맹 성립!` };
  }
  rel.favor = clamp(rel.favor - 5, 0, 100);
  return { ok: true, msg: `${factionName(s, target)}이(가) 동맹을 거절했다. (확률 ${Math.round(p * 100)}%)` };
}

export function sendGift(s: GameState, faction: FactionId, oid: OfficerId, target: FactionId): Result {
  const r = readyOfficer(s, oid, faction);
  if ('ok' in r) return r;
  const o = r;
  const rel = relation(s, faction, target);
  if (!rel) return fail('외교 상대가 없다.');
  const c = s.cities[o.city];
  if (c.gold < 300) return fail('금 300이 필요하다.');
  c.gold -= 300;
  const d = Math.round(5 + o.stats.cha / 10);
  rel.favor = clamp(rel.favor + d, 0, 100);
  o.acted = true;
  return { ok: true, msg: `${factionName(s, target)}의 우호 +${d}` };
}

export function schemeChance(o: Officer, target: Officer) {
  return clamp(0.35 + (o.stats.int - target.stats.int) / 100, 0.05, 0.9);
}

/** 이간: 인접 적 도시 장수의 충성을 떨어뜨린다 */
export function sow(s: GameState, faction: FactionId, oid: OfficerId, targetId: OfficerId): Result {
  const r = readyOfficer(s, oid, faction);
  if ('ok' in r) return r;
  const o = r;
  const t = s.officers[targetId];
  if (!t || t.status !== 'serving' || !t.faction || !isHostile(s, faction, t.faction)) return fail('대상이 적 장수가 아니다.');
  if (!isAdjacent(s, o.city, t.city)) return fail('인접한 도시의 장수만 노릴 수 있다.');
  if (isRuler(s, t)) return fail('군주는 이간할 수 없다.');
  o.acted = true;
  const p = schemeChance(o, t);
  if (chance(s, p)) {
    const d = 8 + Math.round((o.stats.int - t.stats.int) / 5 + 5);
    t.loyalty = clamp(t.loyalty - Math.max(5, d), 0, 100);
    return { ok: true, msg: `${t.name}의 충성이 흔들린다. (충성 ${t.loyalty})` };
  }
  return { ok: true, msg: `${t.name}이(가) 이간을 간파했다. (확률 ${Math.round(p * 100)}%)` };
}

/** 유언비어: 인접 적 도시 민심 하락 */
export function rumor(s: GameState, faction: FactionId, oid: OfficerId, cityId: CityId): Result {
  const r = readyOfficer(s, oid, faction);
  if ('ok' in r) return r;
  const o = r;
  const c = s.cities[cityId];
  if (!c || !c.faction || !isHostile(s, faction, c.faction) || !isAdjacent(s, o.city, cityId)) return fail('인접한 적 도시가 아니다.');
  o.acted = true;
  const gov = c.governor ? s.officers[c.governor] : null;
  const p = clamp(0.4 + (o.stats.int - (gov?.stats.int ?? 50)) / 120, 0.05, 0.9);
  if (chance(s, p)) {
    c.loyalty = clamp(c.loyalty - 10, 0, 100);
    return { ok: true, msg: `${c.name}에 소문이 퍼졌다. 민심 ${c.loyalty}` };
  }
  return { ok: true, msg: `소문이 퍼지지 않았다. (확률 ${Math.round(p * 100)}%)` };
}

export function addReportLog(s: GameState, msg: string) {
  addLog(s, msg);
}
