import type { Battle, CityId, FactionId, GameState, Officer, OfficerId, Side, Tactic } from './types';
import {
  NEUTRAL, addLog, chance, cityName, clamp, cityOfficers, factionCities, factionName, isAdjacent, isHostile,
  neighbors, pick, rand, report,
} from './state';

export const TACTIC_LABEL: Record<Tactic, string> = { charge: '돌격', fire: '화계', defend: '방어', retreat: '퇴각' };

export function leader(s: GameState, side: Side): Officer | null {
  const os = side.officers.map(id => s.officers[id]).filter(Boolean);
  if (!os.length) return null;
  return os.reduce((a, b) => (a.stats.war + a.stats.land > b.stats.war + b.stats.land ? a : b));
}

function bestInt(s: GameState, side: Side): Officer | null {
  const os = side.officers.map(id => s.officers[id]).filter(Boolean);
  if (!os.length) return null;
  return os.reduce((a, b) => (a.stats.int > b.stats.int ? a : b));
}

export interface StartArgs {
  from: CityId; to: CityId; officers: OfficerId[]; troops: number;
}

export function canAttack(s: GameState, faction: FactionId, a: StartArgs): string | null {
  const from = s.cities[a.from];
  const to = s.cities[a.to];
  if (!from || !to) return '도시가 없다.';
  if (from.faction !== faction) return '아군 도시에서만 출진할 수 있다.';
  if (!isAdjacent(s, a.from, a.to)) return '인접한 도시만 공격할 수 있다.';
  if (!isHostile(s, faction, to.faction)) return '동맹·휴전 중인 세력은 공격할 수 없다.';
  if (to.faction === faction) return '아군 도시다.';
  if (!a.officers.length || a.officers.length > 3) return '장수는 1~3명이어야 한다.';
  for (const id of a.officers) {
    const o = s.officers[id];
    if (!o || o.faction !== faction || o.city !== a.from || o.status !== 'serving') return '출진할 수 없는 장수가 있다.';
    if (o.acted) return `${o.name}은(는) 이번 달 이미 움직였다.`;
  }
  if (a.troops < 1000) return '병력은 최소 1,000이 필요하다.';
  if (a.troops > from.troops) return '병력이 부족하다.';
  if (from.food < a.troops * 0.3) return '병량이 부족하다 (병력의 30% 필요).';
  return null;
}

export function startBattle(s: GameState, faction: FactionId, a: StartArgs): Battle {
  const err = canAttack(s, faction, a);
  if (err) throw new Error(err);
  const from = s.cities[a.from];
  const to = s.cities[a.to];
  const food = Math.round(a.troops * 0.3);
  from.troops -= a.troops;
  from.food -= food;
  for (const id of a.officers) s.officers[id].acted = true;
  const defOfficers = to.faction ? cityOfficers(s, to.id, to.faction)
    .sort((x, y) => y.stats.war + y.stats.land - (x.stats.war + x.stats.land)).slice(0, 3).map(o => o.id) : [];
  const b: Battle = {
    attacker: { faction, officers: a.officers, troops: a.troops, start: a.troops, morale: 60 + Math.round(from.training / 4), food },
    defender: { faction: to.faction ?? NEUTRAL, officers: defOfficers, troops: to.troops, start: to.troops, morale: 60 + Math.round(to.training / 4), food: to.food },
    city: to.id,
    from: from.id,
    round: 0,
    maxRounds: 15,
    log: [`${factionName(s, faction)}이(가) ${cityName(s, from.id)}에서 ${to.name}(으)로 진군했다. ${a.troops.toLocaleString()} 대 ${to.troops.toLocaleString()}.`],
    done: false,
    result: null,
  };
  to.troops = 0; // 수비 병력은 전투 중 b.defender가 들고 있다
  return b;
}

function skill(s: GameState, b: Battle, side: Side, o: Officer | null) {
  if (!o) return 30;
  const river = s.cities[b.city].river;
  return river ? (o.stats.naval + o.stats.land) / 2 * 0.5 + o.stats.naval * 0.5 : o.stats.land;
}

function power(s: GameState, b: Battle, side: Side, isDef: boolean): number {
  const l = leader(s, side);
  const war = l?.stats.war ?? 40;
  const city = s.cities[isDef ? b.city : b.from];
  const training = city ? city.training : 50;
  return side.troops * (0.5 + war / 200 + skill(s, b, side, l) / 200) * (0.6 + side.morale / 250) * (0.8 + training / 250);
}

export function aiTactic(s: GameState, b: Battle, isDef: boolean): Tactic {
  const me = isDef ? b.defender : b.attacker;
  const en = isDef ? b.attacker : b.defender;
  if (!isDef && (me.morale < 25 || me.troops < en.troops * 0.35)) return 'retreat';
  const mi = bestInt(s, me)?.stats.int ?? 30;
  const ei = bestInt(s, en)?.stats.int ?? 30;
  if (mi > ei + 8 && chance(s, 0.5)) return 'fire';
  if (isDef && me.troops < en.troops * 0.7) return 'defend';
  return 'charge';
}

function fireChance(s: GameState, me: Side, en: Side) {
  const o = bestInt(s, me);
  const e = bestInt(s, en);
  const bonus = o?.traits.includes('화공') ? 0.15 : 0;
  return clamp(0.3 + ((o?.stats.int ?? 30) - (e?.stats.int ?? 30)) / 150 + bonus, 0.05, 0.85);
}

/** 한 라운드 진행. 플레이어 쪽 전술만 받고 상대는 AI가 고른다 */
export function battleRound(s: GameState, b: Battle, atkTactic?: Tactic, defTactic?: Tactic): void {
  if (b.done) return;
  b.round++;
  const at = atkTactic ?? aiTactic(s, b, false);
  const dt = defTactic ?? aiTactic(s, b, true);
  const A = b.attacker, D = b.defender;
  const line: string[] = [`${b.round}합 — 공격 ${TACTIC_LABEL[at]} / 수비 ${TACTIC_LABEL[dt]}.`];

  if (at === 'retreat') { finish(s, b, 'defender', '공격군이 퇴각했다.'); return; }
  if (dt === 'retreat') { finish(s, b, 'attacker', '수비군이 성을 버리고 달아났다.'); return; }

  // 일기토: 돌격 시 무력 차가 크면 장수끼리 맞붙는다
  if (at === 'charge' || dt === 'charge') {
    const la = leader(s, A), ld = leader(s, D);
    if (la && ld && Math.abs(la.stats.war - ld.stats.war) >= 8 && chance(s, 0.25)) {
      const pA = clamp(0.5 + (la.stats.war - ld.stats.war) / 40, 0.1, 0.9);
      const aWin = chance(s, pA);
      const w = aWin ? la : ld, l = aWin ? ld : la;
      line.push(`일기토! ${w.name}이(가) ${l.name}을(를) 꺾었다.`);
      (aWin ? A : D).morale = clamp((aWin ? A : D).morale + 15, 0, 100);
      (aWin ? D : A).morale = clamp((aWin ? D : A).morale - 25, 0, 100);
    }
  }

  let dmgToD = power(s, b, A, false) * 0.08 * (0.85 + rand(s) * 0.3);
  let dmgToA = power(s, b, D, true) * 0.08 * (0.85 + rand(s) * 0.3);
  const wall = b.defender.faction ? s.cities[b.city].wall : s.cities[b.city].wall * 0.5;

  const mul = (t: Tactic) => (t === 'charge' ? 1.3 : t === 'defend' ? 0.5 : 1);
  const take = (t: Tactic) => (t === 'charge' ? 1.1 : t === 'defend' ? 0.6 : 1);
  dmgToD *= mul(at) * take(dt) * (1 - wall / 300);
  dmgToA *= mul(dt) * take(at) * (b.defender.faction ? 1.15 : 1); // 성내 지리 보정

  if (at === 'fire') {
    if (chance(s, fireChance(s, A, D))) { dmgToD = D.troops * 0.15 + dmgToD * 0.5; D.morale -= 15; line.push('화계가 성공해 수비군이 불길에 휩싸였다.'); }
    else { dmgToD = 0; A.morale -= 5; line.push('화계가 실패했다.'); }
  }
  if (dt === 'fire') {
    if (chance(s, fireChance(s, D, A))) { dmgToA = A.troops * 0.15 + dmgToA * 0.5; A.morale -= 15; line.push('수비군의 화계가 공격군 진영을 태웠다.'); }
    else { dmgToA = 0; D.morale -= 5; line.push('수비군의 화계가 실패했다.'); }
  }
  if (at === 'defend') A.morale += 5;
  if (dt === 'defend') D.morale += 5;

  dmgToD = Math.round(Math.min(D.troops, dmgToD));
  dmgToA = Math.round(Math.min(A.troops, dmgToA));
  D.troops -= dmgToD;
  A.troops -= dmgToA;
  A.morale = clamp(A.morale - Math.round((dmgToA / Math.max(1, A.troops + dmgToA)) * 60) + 2, 0, 100);
  D.morale = clamp(D.morale - Math.round((dmgToD / Math.max(1, D.troops + dmgToD)) * 60) + 2, 0, 100);

  // 병량: 공격군은 라운드마다 소모
  A.food -= Math.round(A.troops * 0.02);
  if (A.food <= 0) { A.food = 0; A.morale = clamp(A.morale - 20, 0, 100); line.push('공격군의 병량이 바닥났다.'); }

  line.push(`피해 — 공격 ${dmgToA.toLocaleString()} / 수비 ${dmgToD.toLocaleString()}. 남은 병력 ${A.troops.toLocaleString()} 대 ${D.troops.toLocaleString()}.`);
  b.log.push(line.join(' '));

  if (D.troops < 300 || D.morale <= 0) return finish(s, b, 'attacker', '수비군이 무너졌다.');
  if (A.troops < 300 || A.morale <= 0) return finish(s, b, 'defender', '공격군이 무너졌다.');
  if (b.round >= b.maxRounds) return finish(s, b, 'defender', '공격군이 성을 함락하지 못하고 물러났다.');
}

export function autoResolve(s: GameState, b: Battle) {
  let guard = 0;
  while (!b.done && guard++ < 50) battleRound(s, b);
}

/** 모의전: 같은 상태를 복제해 여러 번 돌려 승률을 추정한다 (실제 상태는 바뀌지 않음) */
export function simulateWinRate(s: GameState, b: Battle, runs = 20): number {
  let wins = 0;
  for (let i = 0; i < runs; i++) {
    const s2: GameState = JSON.parse(JSON.stringify(s));
    s2.rng = (s.rng + i * 7919) | 0;
    const b2: Battle = JSON.parse(JSON.stringify(b));
    b2.log = [];
    autoResolveNoApply(s2, b2);
    if (b2.result === 'attacker') wins++;
  }
  return wins / runs;
}

function autoResolveNoApply(s: GameState, b: Battle) {
  // finish()가 상태를 바꾸더라도 복제본이므로 안전하다
  autoResolve(s, b);
}

function finish(s: GameState, b: Battle, result: 'attacker' | 'defender', why: string) {
  b.done = true;
  b.result = result;
  b.log.push(why);
  const city = s.cities[b.city];
  const from = s.cities[b.from];
  const A = b.attacker, D = b.defender;
  const aName = factionName(s, A.faction), dName = factionName(s, D.faction || null);

  if (result === 'attacker') {
    const prevFaction = city.faction;
    // 수비 장수 처리: 퇴로가 있으면 도주, 없으면 포로
    const defenders = prevFaction ? cityOfficers(s, city.id, prevFaction) : [];
    const escape = prevFaction ? neighbors(s, city.id).filter(n => s.cities[n].faction === prevFaction) : [];
    const anyOwn = prevFaction ? factionCities(s, prevFaction).filter(c => c.id !== city.id).map(c => c.id) : [];
    for (const o of defenders) {
      const ruler = s.factions[prevFaction!].ruler === o.id;
      if (ruler && anyOwn.length) {
        // 군주는 아군 성이 하나라도 남아 있으면 그곳으로 피신한다
        o.city = escape.length ? pick(s, escape) : pick(s, anyOwn);
      } else if (escape.length && chance(s, 0.5)) {
        o.city = pick(s, escape);
      } else {
        o.status = 'captive';
        s.captives.push({ officer: o.id, by: A.faction });
        b.log.push(`${o.name}이(가) 사로잡혔다.`);
      }
    }
    city.faction = A.faction;
    city.troops = A.troops;
    city.training = from.training;
    city.gold = Math.round(city.gold * 0.5);
    city.food = Math.round(D.food * 0.5) + A.food;
    city.loyalty = clamp(city.loyalty - 10, 0, 100);
    city.policy = A.faction === s.player ? 'balanced' : city.policy;
    for (const id of A.officers) s.officers[id].city = city.id;
    const l = leader(s, A);
    city.governor = l?.id ?? A.officers[0] ?? null;
    s.factions[A.faction].mandate = clamp(s.factions[A.faction].mandate + 5, 0, 100);
    if (prevFaction) s.factions[prevFaction].mandate = clamp(s.factions[prevFaction].mandate - 3, 0, 100);
    report(s, `${aName}이(가) ${dName}의 ${city.name}을(를) 빼앗았다.`);
    s.chronicle.push(`${s.year}년 ${s.month}월, ${aName} ${city.name} 함락.`);
    addLog(s, `${city.name} 함락 — ${aName}`, A.faction === s.player ? 'good' : prevFaction === s.player ? 'bad' : 'info');
    if (prevFaction) checkFactionAlive(s, prevFaction, A.faction);
  } else {
    // 공격 실패: 남은 병력과 장수는 출발 도시로 귀환
    from.troops += A.troops;
    from.food += A.food;
    city.troops = D.troops;
    city.food = D.food;
    report(s, `${aName}의 ${city.name} 공격은 실패로 끝났다.`);
    addLog(s, `${city.name} 방어 성공 — ${dName}`, D.faction === s.player ? 'good' : A.faction === s.player ? 'bad' : 'info');
  }
}

export function checkFactionAlive(s: GameState, f: FactionId, by: FactionId) {
  if (factionCities(s, f).length > 0) return;
  const fac = s.factions[f];
  fac.alive = false;
  for (const o of Object.values(s.officers)) {
    if (o.faction === f && o.status === 'serving') {
      if (o.id === fac.ruler) {
        o.status = 'captive';
        s.captives.push({ officer: o.id, by });
      } else {
        o.status = 'free';
        o.faction = null;
        o.found = true;
      }
    }
  }
  report(s, `${fac.name}이(가) 멸망했다.`);
  s.chronicle.push(`${s.year}년 ${s.month}월, ${fac.name} 멸망.`);
}
