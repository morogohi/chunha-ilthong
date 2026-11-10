import { newGame, endTurn, aiPlaysPlayer, factionCities, startBattle, autoResolve } from '../src/core';
it('dbg', () => {
  const s = newGame('cao', 1013);
  for (let m = 0; m < 120; m++) {
    aiPlaysPlayer(s); endTurn(s);
    if (m % 12 === 11) {
      const row = Object.values(s.factions).filter(f=>f.alive).map(f => {
        const cs = factionCities(s, f.id);
        return `${f.id}:${cs.length}c/${Math.round(cs.reduce((a,c)=>a+c.troops,0)/1000)}k/g${Math.round(cs.reduce((a,c)=>a+c.gold,0))}/f${Math.round(cs.reduce((a,c)=>a+c.food,0)/1000)}k`;
      });
      console.log(s.year, row.join(' '));
    }
  }
  // 공격 성공률 표본: 1.3배 병력 공격
  for (const r of [0.8, 1.0, 1.3, 1.6, 2.0]) {
  let w = 0;
  for (let i = 0; i < 100; i++) {
    const t = newGame('cao', i + 1);
    t.cities.xuchang.troops = Math.round(20000*r); t.cities.xuchang.food = 99999; t.cities.wan.troops = 20000; t.cities.wan.wall = 70;
    const b = startBattle(t, 'cao', { from: 'xuchang', to: 'wan', officers: ['xuchu','xunyu'], troops: Math.round(20000*r) });
    autoResolve(t, b); if (b.result === 'attacker') w++;
  }
  console.log('ratio', r, w / 100);
  }
});
