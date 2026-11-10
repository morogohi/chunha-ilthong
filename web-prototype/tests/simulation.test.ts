// AI끼리 붙여 30년(360턴)을 돌리며 불변식이 깨지지 않는지 확인하고, 밸런스 지표를 남긴다.
import { writeFileSync, mkdirSync } from 'node:fs';
import { newGame, endTurn, aiPlaysPlayer, factionCities, FACTION_ORDER } from '../src/core';
import type { GameState } from '../src/core';

function invariants(s: GameState): string[] {
  const errs: string[] = [];
  for (const c of Object.values(s.cities)) {
    if (c.troops < 0) errs.push(`${c.id} troops<0`);
    if (c.gold < 0) errs.push(`${c.id} gold<0`);
    if (c.food < 0) errs.push(`${c.id} food<0`);
    if (c.loyalty < 0 || c.loyalty > 100) errs.push(`${c.id} loyalty`);
    if (c.wall < 0 || c.wall > 100) errs.push(`${c.id} wall`);
    if (!Number.isFinite(c.troops + c.gold + c.food + c.agri + c.commerce)) errs.push(`${c.id} NaN`);
  }
  for (const o of Object.values(s.officers)) {
    if (o.status === 'serving') {
      if (!o.faction) errs.push(`${o.id} serving without faction`);
      else if (s.cities[o.city].faction !== o.faction) errs.push(`${o.id} in foreign city ${o.city}`);
    }
    if (o.status === 'captive' && !s.captives.some(c => c.officer === o.id)) errs.push(`${o.id} captive without record`);
  }
  for (const f of Object.values(s.factions)) {
    if (f.alive && factionCities(s, f.id).length === 0) errs.push(`${f.id} alive without cities`);
    if (f.alive && s.officers[f.ruler].status !== 'serving') errs.push(`${f.id} ruler not serving`);
  }
  return errs;
}

interface Run { player: string; seed: number; months: number; winner: string | null; over: boolean; aliveAtEnd: number; firstFall: number | null; captures: number }

describe('AI 자동 시뮬레이션 (6세력 × 시드 8개 × 최대 30년)', () => {
  const runs: Run[] = [];
  for (const player of FACTION_ORDER) {
    for (let seed = 1; seed <= 8; seed++) {
      it(`${player} seed ${seed}`, () => {
        const s = newGame(player, seed * 1013);
        let firstFall: number | null = null;
        let m = 0;
        for (; m < 360 && !s.over; m++) {
          aiPlaysPlayer(s);
          endTurn(s);
          const errs = invariants(s);
          expect(errs, `month ${m}: ${errs.join('; ')}`).toEqual([]);
          if (firstFall === null && Object.values(s.factions).some(f => !f.alive)) firstFall = m;
        }
        runs.push({
          player, seed, months: m, winner: s.winner, over: s.over,
          aliveAtEnd: Object.values(s.factions).filter(f => f.alive).length,
          firstFall, captures: s.chronicle.filter(x => x.includes('함락')).length,
        });
      });
    }
  }
  afterAll(() => {
    mkdirSync('reports', { recursive: true });
    const unified = runs.filter(r => r.over && r.winner);
    const summary = {
      runs: runs.length,
      unifiedRate: unified.length / runs.length,
      avgMonthsToUnify: unified.length ? Math.round(unified.reduce((a, r) => a + r.months, 0) / unified.length) : null,
      playerWins: runs.filter(r => r.winner === r.player).length,
      playerEliminated: runs.filter(r => r.over && r.winner !== r.player).length,
      avgAliveAtEnd: +(runs.reduce((a, r) => a + r.aliveAtEnd, 0) / runs.length).toFixed(2),
      avgFirstFallMonth: Math.round(runs.filter(r => r.firstFall !== null).reduce((a, r) => a + (r.firstFall ?? 0), 0) / Math.max(1, runs.filter(r => r.firstFall !== null).length)),
      avgCityCaptures: +(runs.reduce((a, r) => a + r.captures, 0) / runs.length).toFixed(1),
      winsByFaction: Object.fromEntries(FACTION_ORDER.map(f => [f, runs.filter(r => r.winner === f).length])),
      runsDetail: runs,
    };
    writeFileSync('reports/simulation.json', JSON.stringify(summary, null, 2));
  });
});
