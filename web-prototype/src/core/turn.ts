import type { City, FactionId, GameState, Officer } from './types';
import {
  absMonth, addLog, chance, cityOfficers, clamp, factionCities, factionName, freeOfficers,
  isHostile, isRuler, neighbors, pick, rand, relation, report,
} from './state';
import { domestic, type DomesticCmd, recruit, releaseCaptive, search, proposeAlliance, transport } from './commands';
import { autoResolve, canAttack, leader, startBattle } from './battle';

/** 개전 후 AI가 플레이어를 공격하지 않는 유예 기간 (개월) */
export const GRACE_MONTHS = 6;

const best = (os: Officer[], k: keyof Officer['stats']) => os.slice().sort((a, b) => b.stats[k] - a.stats[k]);

export function isFrontier(s: GameState, c: City): boolean {
  return neighbors(s, c.id).some(n => {
    const nc = s.cities[n];
    return nc.faction !== c.faction && isHostile(s, c.faction, nc.faction);
  });
}

function threat(s: GameState, c: City): number {
  let t = 0;
  for (const n of neighbors(s, c.id)) {
    const nc = s.cities[n];
    if (nc.faction && nc.faction !== c.faction && isHostile(s, c.faction, nc.faction)) t = Math.max(t, nc.troops);
  }
  return t;
}

/** 도시 운영 방침에 따라 아직 움직이지 않은 장수에게 일을 맡긴다 (플레이어 위임·AI 공용) */
export function autoDomestic(s: GameState, c: City, mode: City['policy']) {
  if (!c.faction || mode === 'direct') return;
  const f = c.faction;
  const idle = () => cityOfficers(s, c.id, f).filter(o => !o.acted);
  let guard = 0;
  while (idle().length && guard++ < 10) {
    const os = idle();
    const fr = isFrontier(s, c);
    const thr = threat(s, c);
    let cmd: DomesticCmd;
    const rich = c.gold > 1500 && c.troops < c.pop * 400 - 1500;
    if (mode === 'military' || (mode === 'balanced' && ((fr && c.troops < thr * 1.2) || rich))) {
      if (c.troops < c.pop * 400 - 500 && c.gold > 400) cmd = 'draft';
      else if (c.training < 80 && c.troops > 2000) cmd = 'train';
      else if (c.wall < 90 && c.gold > 100) cmd = 'wall';
      else cmd = 'train';
    } else if (mode === 'welfare') {
      cmd = c.loyalty < 90 && c.food > 3000 ? 'relief' : c.flood < 80 ? 'dike' : 'trade';
    } else if (mode === 'develop') {
      cmd = c.agri <= c.commerce ? 'farm' : 'trade';
    } else {
      if (c.loyalty < 55 && c.food > 3000) cmd = 'relief';
      else if (c.flood < 40) cmd = 'dike';
      else if (c.training < 60 && c.troops > 3000 && fr) cmd = 'train';
      else cmd = c.agri <= c.commerce ? 'farm' : 'trade';
    }
    const statKey = cmd === 'relief' || cmd === 'draft' ? 'cha' : cmd === 'train' ? 'land' : 'pol';
    const o = best(os, statKey)[0];
    const r = domestic(s, f, o.id, cmd);
    if (!r.ok) {
      // 다른 일이라도 시도하고, 안 되면 쉬게 한다
      const alt: DomesticCmd = c.troops > 1000 && c.training < 100 ? 'train' : 'farm';
      const r2 = domestic(s, f, o.id, alt);
      if (!r2.ok) o.acted = true;
    }
  }
}

function aiFaction(s: GameState, f: FactionId) {
  const cities = factionCities(s, f);
  // 1) 인재: 탐색과 등용
  for (const c of cities) {
    const os = cityOfficers(s, c.id, f).filter(o => !o.acted);
    if (!os.length) continue;
    const found = freeOfficers(s, c.id).filter(o => o.found && o.special !== 'thrice_visit');
    if (found.length) recruit(s, f, best(os, 'cha')[0].id, found[0].id);
    else if (chance(s, 0.25) && freeOfficers(s, c.id).length) search(s, f, best(os, 'int')[0].id);
  }
  // 2) 포로 처리
  for (const cap of s.captives.filter(c => c.by === f)) {
    const t = s.officers[cap.officer];
    const c = factionCities(s, f)[0];
    const os = c ? cityOfficers(s, c.id, f).filter(o => !o.acted) : [];
    if (os.length && chance(s, 0.6)) {
      t.city = c.id;
      const r = recruit(s, f, best(os, 'cha')[0].id, t.id);
      if (r.ok && t.status === 'serving') continue;
    }
    releaseCaptive(s, f, t.id, chance(s, 0.1));
  }
  // 3) 공격
  for (const c of factionCities(s, f)) {
    const os = cityOfficers(s, c.id, f).filter(o => !o.acted);
    if (!os.length || c.troops < 6000) continue;
    const grace = absMonth(s) - (200 * 12) < GRACE_MONTHS;
    const targets = neighbors(s, c.id).map(n => s.cities[n])
      .filter(t => t.faction !== f && isHostile(s, f, t.faction) && !(grace && t.faction === s.player));
    if (!targets.length) continue;
    const myLead = best(os, 'war').slice(0, 3);
    const myPow = c.troops * 0.8 * (0.6 + c.training / 200) * (0.7 + (myLead[0]?.stats.war ?? 50) / 300);
    const scored = targets
      .map(t => {
        const dl = t.faction ? leader(s, { faction: t.faction, officers: cityOfficers(s, t.id, t.faction).map(o => o.id), troops: 0, morale: 0, food: 0 }) : null;
        const defPow = Math.max(500, t.troops) * (0.6 + t.training / 200) * (0.7 + (dl?.stats.war ?? 40) / 300) * (1 + t.wall / 200);
        return { t, ratio: myPow / defPow };
      })
      .sort((a, b) => b.ratio - a.ratio);
    const pickT = scored[0];
    // 다른 적이 노리고 있으면 성을 비우지 않는다
    const otherThreat = pickT ? Math.max(0, ...neighbors(s, c.id).map(n => s.cities[n])
      .filter(n => n.id !== pickT.t.id && n.faction && n.faction !== f && isHostile(s, f, n.faction)).map(n => n.troops * 0.6)) : 0;
    const troops = Math.floor(Math.min(c.troops * 0.8, c.troops - otherThreat));
    if (pickT && pickT.ratio * (troops / (c.troops * 0.8)) > 1.15 && troops >= 3000 && chance(s, 0.5)) {
      const args = { from: c.id, to: pickT.t.id, officers: myLead.map(o => o.id), troops };
      if (!canAttack(s, f, args)) {
        const b = startBattle(s, f, args);
        autoResolve(s, b);
      }
    }
  }
  // 4) 후방 → 전방 병력 이동
  for (const c of factionCities(s, f)) {
    if (isFrontier(s, c) || c.troops < 6000) continue;
    const os = cityOfficers(s, c.id, f).filter(o => !o.acted);
    if (!os.length) continue;
    const dst = neighbors(s, c.id).map(n => s.cities[n]).filter(n => n.faction === f && isFrontier(s, n));
    if (dst.length) transport(s, f, os[0].id, pick(s, dst).id, Math.floor(c.troops * 0.5), 0, Math.floor(c.food * 0.3));
  }
  // 5) 외교: 같은 적을 둔 세력과 가끔 동맹
  if (chance(s, 0.08)) {
    const others = Object.values(s.factions).filter(x => x.alive && x.id !== f && x.id !== s.player);
    const t = others.length ? pick(s, others) : null;
    const rel = t ? relation(s, f, t.id) : null;
    const c = factionCities(s, f).find(x => x.gold > 600);
    const o = c ? best(cityOfficers(s, c.id, f).filter(x => !x.acted), 'cha')[0] : null;
    if (t && rel && rel.alliance <= 0 && rel.favor > 45 && o) proposeAlliance(s, f, o.id, t.id);
  }
  // 6) 남은 장수는 내정
  for (const c of factionCities(s, f)) autoDomestic(s, c, 'balanced');
}

function economy(s: GameState) {
  for (const c of Object.values(s.cities)) {
    const tax = Math.round((c.commerce / 10) * (0.5 + c.loyalty / 200));
    if (c.faction) c.gold += tax;
    if (s.month === 7) {
      const harvest = Math.round(c.agri * 40 * (0.5 + c.loyalty / 200));
      c.food += harvest;
      if (c.faction === s.player) report(s, `${c.name}에서 병량 ${harvest.toLocaleString()}을(를) 거두었다.`);
    }
    const eat = Math.round(c.troops / 60);
    c.food -= eat;
    if (c.food < 0) {
      const lost = Math.round(c.troops * 0.1);
      c.troops -= lost;
      c.food = 0;
      c.loyalty = clamp(c.loyalty - 3, 0, 100);
      if (c.faction === s.player) report(s, `${c.name}의 병량이 떨어져 병사 ${lost.toLocaleString()}이(가) 흩어졌다.`);
    }
    // 민심은 천천히 50 쪽으로 돌아간다
    if (c.loyalty > 70) c.loyalty -= 1;
    if (c.loyalty < 40) c.loyalty += 1;
    c.pop = clamp(c.pop + (c.loyalty > 60 ? 0.2 : -0.1), 20, 200);
  }
}

const EVENT_TEXT = {
  locust: { warn: '메뚜기 떼가 일어날 조짐이 있다', hit: '메뚜기 떼가 들판을 휩쓸었다' },
  flood: { warn: '강물이 불어나고 있다', hit: '큰물이 졌다' },
  plague: { warn: '역병의 소문이 돈다', hit: '역병이 돌았다' },
} as const;

function events(s: GameState) {
  const now = absMonth(s);
  // 발생
  for (const e of s.pending.filter(p => p.month <= now)) {
    const c = s.cities[e.city];
    if (e.kind === 'locust') { c.food = Math.round(c.food * 0.6); c.agri = Math.round(c.agri * 0.95); }
    if (e.kind === 'flood') {
      const m = 1 - (1 - c.flood / 100) * 0.25;
      c.agri = Math.round(c.agri * m); c.commerce = Math.round(c.commerce * m);
      c.wall = clamp(c.wall - Math.round((100 - c.flood) / 10), 0, 100);
    }
    if (e.kind === 'plague') { c.troops = Math.round(c.troops * 0.9); c.pop = c.pop * 0.95; }
    if (c.faction === s.player) report(s, `${s.month}월, ${c.name}에 ${EVENT_TEXT[e.kind].hit}.`);
  }
  s.pending = s.pending.filter(p => p.month > now);
  // 다음 달 재해 예고
  for (const c of Object.values(s.cities)) {
    if (!chance(s, 0.025)) continue;
    const kinds: ('locust' | 'flood' | 'plague')[] = c.river ? ['flood', 'flood', 'locust', 'plague'] : ['locust', 'plague'];
    const kind = pick(s, kinds);
    s.pending.push({ kind, city: c.id, month: now + 1 });
    if (c.faction === s.player) report(s, `${c.name}: ${EVENT_TEXT[kind].warn}. 다음 달을 대비하라.`);
  }
}

function loyaltyAndDefection(s: GameState) {
  for (const o of Object.values(s.officers)) {
    if (o.status !== 'serving' || !o.faction || isRuler(s, o)) continue;
    const ruler = s.officers[s.factions[o.faction].ruler];
    const aff = Math.min(Math.abs(ruler.affinity - o.affinity), 150 - Math.abs(ruler.affinity - o.affinity));
    if (aff < 15 && o.loyalty < 100 && chance(s, 0.2)) o.loyalty++;
    if (o.loyalty < 40 && o.ambition > 50 && chance(s, 0.15)) {
      const f = o.faction;
      o.status = 'free'; o.faction = null; o.found = true;
      const c = s.cities[o.city];
      if (c.governor === o.id) c.governor = null;
      if (f === s.player) report(s, `${o.name}이(가) 충성을 저버리고 떠났다.`);
      addLog(s, `${o.name} 출분 (${factionName(s, f)})`, f === s.player ? 'bad' : 'info');
    }
  }
  // 태수 공석 채우기
  for (const c of Object.values(s.cities)) {
    if (!c.faction) continue;
    const os = cityOfficers(s, c.id, c.faction);
    if (!c.governor || !os.some(o => o.id === c.governor)) c.governor = os[0]?.id ?? null;
  }
}

function relationsTick(s: GameState) {
  for (const r of s.relations) {
    if (r.alliance > 0) {
      r.alliance--;
      if (r.alliance === 0 && (r.a === s.player || r.b === s.player)) report(s, `${factionName(s, r.a === s.player ? r.b : r.a)}과(와)의 동맹이 끝났다.`);
    }
    if (r.truce > 0) r.truce--;
  }
}

function historianLine(s: GameState): string {
  const mine = factionCities(s, s.player);
  const troops = mine.reduce((a, c) => a + c.troops, 0);
  const gold = mine.reduce((a, c) => a + c.gold, 0);
  const lines = [
    `${s.year}년 ${s.month}월. ${factionName(s, s.player)}은(는) ${mine.length}성, 병력 ${troops.toLocaleString()}, 금 ${gold.toLocaleString()}을 헤아린다.`,
  ];
  const strongest = Object.values(s.factions).filter(f => f.alive && f.id !== s.player)
    .map(f => ({ f, n: factionCities(s, f.id).length }))
    .sort((a, b) => b.n - a.n)[0];
  if (strongest) lines.push(`가장 큰 적은 ${strongest.f.name}(${strongest.n}성)이다.`);
  return lines.join(' ');
}

function checkEnd(s: GameState) {
  const owners = new Set(Object.values(s.cities).map(c => c.faction));
  const alive = Object.values(s.factions).filter(f => f.alive);
  if (factionCities(s, s.player).length === 0) {
    s.over = true; s.winner = null;
    report(s, '모든 성을 잃었다. 기록은 여기서 끝난다.');
    return;
  }
  if (owners.size === 1 && owners.has(s.player)) {
    s.over = true; s.winner = s.player;
    report(s, `${factionName(s, s.player)}이(가) 천하를 하나로 묶었다.`);
    s.chronicle.push(`${s.year}년 ${s.month}월, ${factionName(s, s.player)} 천하통일.`);
    return;
  }
  if (alive.length === 1 && alive[0].id !== s.player) { s.over = true; s.winner = alive[0].id; }
}

/** 달을 넘긴다. 플레이어 명령이 끝난 뒤 호출한다 */
export function endTurn(s: GameState) {
  if (s.over) return;
  s.report = [];
  // 플레이어 위임 도시
  for (const c of factionCities(s, s.player)) autoDomestic(s, c, c.policy);
  // 플레이어가 결정하지 않은 포로는 석방
  for (const cap of s.captives.filter(c => c.by === s.player)) releaseCaptive(s, s.player, cap.officer, false);
  // AI 세력 (매달 순서를 섞는다)
  const ai = Object.values(s.factions).filter(f => f.alive && f.id !== s.player).map(f => f.id);
  for (let i = ai.length - 1; i > 0; i--) { const j = Math.floor(rand(s) * (i + 1)); [ai[i], ai[j]] = [ai[j], ai[i]]; }
  for (const f of ai) if (s.factions[f].alive) aiFaction(s, f);
  economy(s);
  events(s);
  loyaltyAndDefection(s);
  relationsTick(s);
  for (const o of Object.values(s.officers)) o.acted = false;
  s.month++;
  if (s.month > 12) { s.month = 1; s.year++; }
  checkEnd(s);
  s.report.unshift(historianLine(s));
}

/** 플레이어 없이 AI끼리 돌릴 때 쓰는 헬퍼 (시뮬레이션 테스트용) */
export function aiPlaysPlayer(s: GameState) {
  aiFaction(s, s.player);
}
