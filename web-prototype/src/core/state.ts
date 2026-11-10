import type { City, CityId, FactionId, GameState, Officer, Relation } from './types';
import { FACTION_ORDER, makeCities, makeFactions, makeOfficers, makeRelations, ROADS, SCENARIO } from './data';

export const NEUTRAL = '';

export function rand(s: GameState): number {
  s.rng = (s.rng + 0x6d2b79f5) | 0;
  let t = s.rng;
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}

export function chance(s: GameState, p: number): boolean {
  return rand(s) < Math.max(0, Math.min(1, p));
}

export function pick<T>(s: GameState, arr: T[]): T {
  return arr[Math.floor(rand(s) * arr.length)];
}

export const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, v));

export function newGame(player: FactionId, seed = Date.now() % 2147483647): GameState {
  const s: GameState = {
    version: 1,
    seed,
    rng: seed | 0,
    year: SCENARIO.year,
    month: SCENARIO.month,
    player,
    cities: makeCities(),
    officers: makeOfficers(),
    factions: makeFactions(),
    relations: makeRelations(FACTION_ORDER),
    roads: ROADS,
    pending: [],
    log: [],
    report: [],
    captives: [],
    chronicle: [],
    winner: null,
    over: false,
  };
  for (const c of Object.values(s.cities)) if (c.faction === player) c.policy = c.governor === s.factions[player].ruler ? 'direct' : 'balanced';
  s.report = [`${SCENARIO.year}년 1월, 시나리오 「${SCENARIO.title}」를 연다. ${s.factions[player].name}의 기록을 시작한다.`];
  return s;
}

export const absMonth = (s: GameState) => s.year * 12 + (s.month - 1);

export function neighbors(s: GameState, id: CityId): CityId[] {
  const out: CityId[] = [];
  for (const [a, b] of s.roads) {
    if (a === id) out.push(b);
    else if (b === id) out.push(a);
  }
  return out;
}

export function isAdjacent(s: GameState, a: CityId, b: CityId) {
  return neighbors(s, a).includes(b);
}

export function factionCities(s: GameState, f: FactionId): City[] {
  return Object.values(s.cities).filter(c => c.faction === f);
}

export function cityOfficers(s: GameState, city: CityId, faction?: FactionId | null): Officer[] {
  return Object.values(s.officers).filter(o => o.city === city && o.status === 'serving' && (faction === undefined || o.faction === faction));
}

export function factionOfficers(s: GameState, f: FactionId): Officer[] {
  return Object.values(s.officers).filter(o => o.faction === f && o.status === 'serving');
}

export function freeOfficers(s: GameState, city: CityId): Officer[] {
  return Object.values(s.officers).filter(o => o.city === city && o.status === 'free');
}

export function relation(s: GameState, a: FactionId, b: FactionId): Relation | undefined {
  return s.relations.find(r => (r.a === a && r.b === b) || (r.a === b && r.b === a));
}

export function isHostile(s: GameState, a: FactionId | null, b: FactionId | null): boolean {
  if (!a || !b) return true; // 공백지는 누구에게나 공격 대상
  if (a === b) return false;
  const r = relation(s, a, b);
  return !r || (r.alliance <= 0 && r.truce <= 0);
}

export function addLog(s: GameState, text: string, tone: 'good' | 'bad' | 'info' = 'info') {
  s.log.push({ t: absMonth(s), text, tone });
  if (s.log.length > 300) s.log.splice(0, s.log.length - 300);
}

export function report(s: GameState, text: string) {
  s.report.push(text);
}

export function age(s: GameState, o: Officer) {
  return s.year - o.born;
}

export function cityName(s: GameState, id: CityId) {
  return s.cities[id]?.name ?? id;
}

export function factionName(s: GameState, id: FactionId | null) {
  if (!id) return '공백지';
  return s.factions[id]?.name ?? id;
}

export function isRuler(s: GameState, o: Officer) {
  return !!o.faction && s.factions[o.faction]?.ruler === o.id;
}
