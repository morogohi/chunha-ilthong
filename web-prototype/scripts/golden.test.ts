import { writeFileSync, mkdirSync } from 'node:fs';
import { newGame, endTurn, aiPlaysPlayer, startBattle, battleRound } from '../src/core';
import type { GameState } from '../src/core';

const f3 = (n: number) => n.toFixed(3);
export function signature(s: GameState): string {
  const cs = Object.values(s.cities).map(c => [c.id, c.faction ?? '', f3(c.troops), f3(c.gold), f3(c.food), f3(c.loyalty), f3(c.agri), f3(c.commerce), f3(c.wall), f3(c.training), f3(c.flood)].join(':')).join(';');
  const os = Object.values(s.officers).map(o => `${o.id}:${o.status}:${o.faction ?? ''}:${o.city}:${f3(o.loyalty)}`).join(';');
  const fs = Object.values(s.factions).map(f => `${f.id}:${f.alive}:${f3(f.mandate)}`).join(';');
  return `${s.year}-${s.month}|rng=${s.rng}|${cs}|${os}|${fs}|cap=${s.captives.length}|pend=${s.pending.length}`;
}

it('golden traces', () => {
  const traces: Record<string, string[]> = {};
  const cases: [string, number][] = [['cao', 101], ['liu', 202], ['sun', 303], ['yuan', 404], ['biao', 505], ['ma', 606]];
  for (const [f, seed] of cases) {
    const s = newGame(f, seed);
    const out: string[] = [signature(s)];
    for (let m = 0; m < 120 && !s.over; m++) { aiPlaysPlayer(s); endTurn(s); out.push(signature(s)); }
    traces[`${f}-${seed}`] = out;
  }
  // 전투 단독: 정해진 전술 순서
  const s = newGame('cao', 4242);
  s.cities.xuchang.troops = 30000; s.cities.xuchang.food = 50000;
  const b = startBattle(s, 'cao', { from: 'xuchang', to: 'wan', officers: ['xuchu', 'guojia', 'caocao'], troops: 24000 });
  const seq = ['charge', 'fire', 'defend', 'charge', 'fire', 'charge', 'charge', 'charge', 'charge', 'charge', 'charge', 'charge', 'charge', 'charge', 'charge'] as const;
  const bt: string[] = [];
  for (const t of seq) { if (b.done) break; battleRound(s, b, t); bt.push(`${b.round}|${f3(b.attacker.troops)}|${f3(b.defender.troops)}|${f3(b.attacker.morale)}|${f3(b.defender.morale)}|${f3(b.attacker.food)}|${b.done}|${b.result}`); }
  bt.push(signature(s));
  traces['battle-4242'] = bt;
  mkdirSync('reports', { recursive: true });
  writeFileSync('reports/golden.json', JSON.stringify(traces));
});
