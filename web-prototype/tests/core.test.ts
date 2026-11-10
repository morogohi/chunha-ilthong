import {
  newGame, endTurn, aiPlaysPlayer, domestic, search, recruit, recruitChance, startBattle, battleRound, autoResolve,
  canAttack, proposeAlliance, relation, serialize, deserialize, History, neighbors, factionCities, cityOfficers,
  releaseCaptive, sow, moveOfficer, transport, simulateWinRate,
} from '../src/core';
import type { GameState } from '../src/core';

const fresh = (p = 'cao', seed = 42) => newGame(p, seed);

describe('데이터 무결성', () => {
  const s = fresh();
  it('모든 장수의 소재 도시가 존재한다', () => {
    for (const o of Object.values(s.officers)) expect(s.cities[o.city], o.id).toBeDefined();
  });
  it('도로가 모두 실제 도시를 잇고 지도가 하나로 연결된다', () => {
    for (const [a, b] of s.roads) { expect(s.cities[a]).toBeDefined(); expect(s.cities[b]).toBeDefined(); }
    const seen = new Set(['ye']); const q = ['ye'];
    while (q.length) for (const n of neighbors(s, q.shift()!)) if (!seen.has(n)) { seen.add(n); q.push(n); }
    expect(seen.size).toBe(Object.keys(s.cities).length);
  });
  it('군주는 자기 세력 도시에 있다', () => {
    for (const f of Object.values(s.factions)) {
      const r = s.officers[f.ruler];
      expect(r.faction).toBe(f.id);
      expect(s.cities[r.city].faction).toBe(f.id);
    }
  });
  it('능력치는 1~100이고, 95 이상은 능력치마다 3명 이하다 (산정 기준)', () => {
    const keys = ['war', 'int', 'pol', 'cha', 'land', 'naval'] as const;
    for (const k of keys) {
      const top = Object.values(s.officers).filter(o => o.stats[k] >= 95);
      expect(top.length, `${k}: ${top.map(o => o.name).join(',')}`).toBeLessThanOrEqual(3);
      for (const o of Object.values(s.officers)) { expect(o.stats[k]).toBeGreaterThanOrEqual(1); expect(o.stats[k]).toBeLessThanOrEqual(100); }
    }
  });
  it('12성 6세력 구성이다', () => {
    expect(Object.keys(s.cities)).toHaveLength(12);
    expect(Object.keys(s.factions)).toHaveLength(6);
  });
});

describe('내정', () => {
  it('개간은 금 50을 쓰고 농업을 올리며, 한 달에 한 번만 행동한다', () => {
    const s = fresh();
    const c = s.cities.xuchang; const g = c.gold; const a = c.agri;
    const r = domestic(s, 'cao', 'xunyu', 'farm');
    expect(r.ok).toBe(true);
    expect(c.gold).toBe(g - 50);
    expect(c.agri).toBeGreaterThan(a);
    expect(domestic(s, 'cao', 'xunyu', 'trade').ok).toBe(false);
  });
  it('다른 세력 장수에게는 명령할 수 없다', () => {
    const s = fresh();
    expect(domestic(s, 'cao', 'yuanshao', 'farm').ok).toBe(false);
  });
  it('징병은 인구 상한과 금을 지킨다', () => {
    const s = fresh();
    const c = s.cities.runan; c.gold = 10;
    expect(domestic(s, 'liu', 'liubei', 'draft').ok).toBe(false);
    c.gold = 99999; c.troops = c.pop * 400 - 100;
    expect(domestic(s, 'liu', 'guanyu', 'draft').ok).toBe(false);
  });
  it('징병하면 훈련도가 평균으로 섞여 내려간다', () => {
    const s = fresh();
    const c = s.cities.xuchang; c.training = 80; c.troops = 10000;
    domestic(s, 'cao', 'caocao', 'draft');
    expect(c.training).toBeLessThan(80);
    expect(c.troops).toBeGreaterThan(10000);
  });
});

describe('인사', () => {
  it('탐색으로 재야 인재를 찾고 등용 확률은 5~95% 사이다', () => {
    const s = fresh('cao', 7);
    // 낙양의 사마의를 찾을 때까지 탐색
    let found = false;
    for (let m = 0; m < 30 && !found; m++) {
      search(s, 'cao', 'zhangliao');
      found = s.officers.simayi.found;
      s.officers.zhangliao.acted = false;
    }
    expect(found).toBe(true);
    const p = recruitChance(s, s.officers.zhangliao, s.officers.simayi, 'cao');
    expect(p).toBeGreaterThanOrEqual(0.05);
    expect(p).toBeLessThanOrEqual(0.95);
  });
  it('삼고초려: 군주가 세 번 찾아가야 제갈량이 출사한다', () => {
    const s = fresh('liu');
    s.cities.xiangyang.faction = 'liu';
    s.officers.liubei.city = 'xiangyang';
    expect(recruit(s, 'liu', 'liubei', 'zhugeliang').ok).toBe(false);
    for (let i = 0; i < 3; i++) {
      s.officers.liubei.acted = false;
      search(s, 'liu', 'liubei');
    }
    expect(s.officers.zhugeliang.status).toBe('serving');
    expect(s.officers.zhugeliang.faction).toBe('liu');
  });
  it('군주가 아닌 장수는 삼고초려를 대신할 수 없다', () => {
    const s = fresh('liu');
    s.cities.xiangyang.faction = 'liu';
    s.officers.guanyu.city = 'xiangyang';
    for (let i = 0; i < 5; i++) { s.officers.guanyu.acted = false; search(s, 'liu', 'guanyu'); }
    expect(s.officers.zhugeliang.status).toBe('free');
  });
  it('이동과 수송은 인접한 아군 도시로만 된다', () => {
    const s = fresh();
    expect(moveOfficer(s, 'cao', 'xuchu', 'ye').ok).toBe(false);
    expect(moveOfficer(s, 'cao', 'xuchu', 'luoyang').ok).toBe(true);
    const t0 = s.cities.puyang.troops;
    expect(transport(s, 'cao', 'xunyou', 'xuchang', 5000, 0, 0).ok).toBe(true);
    expect(s.cities.puyang.troops).toBe(t0 - 5000);
  });
  it('처형하면 천명이 떨어진다', () => {
    const s = fresh();
    s.officers.yanliang.status = 'captive';
    s.captives.push({ officer: 'yanliang', by: 'cao' });
    const m = s.factions.cao.mandate;
    releaseCaptive(s, 'cao', 'yanliang', true);
    expect(s.officers.yanliang.status).toBe('dead');
    expect(s.factions.cao.mandate).toBeLessThan(m);
  });
});

describe('외교·계략', () => {
  it('동맹 중인 세력은 공격할 수 없다', () => {
    const s = fresh('liu');
    relation(s, 'liu', 'cao')!.alliance = 5;
    expect(canAttack(s, 'liu', { from: 'runan', to: 'xuchang', officers: ['guanyu'], troops: 3000 })).toMatch(/동맹/);
  });
  it('동맹 제안은 예물 금 300을 쓴다', () => {
    const s = fresh('liu');
    const g = s.cities.runan.gold;
    proposeAlliance(s, 'liu', 'sunqian', 'yuan');
    expect(s.cities.runan.gold).toBe(g - 300);
  });
  it('이간은 인접 도시 적 장수만 노린다', () => {
    const s = fresh();
    expect(sow(s, 'cao', 'guojia', 'zhouyu').ok).toBe(false); // 허창-시상 비인접
    const r = sow(s, 'cao', 'guojia', 'guanyu'); // 허창-여남 인접
    expect(r.ok).toBe(true);
  });
});

describe('전투', () => {
  const setup = () => {
    const s = fresh('cao', 3);
    s.cities.xuchang.troops = 40000;
    s.cities.xuchang.food = 50000;
    s.cities.runan.troops = 3000;
    return s;
  };
  it('압도적인 공격군은 성을 빼앗고 장수를 포로로 잡거나 쫓아낸다', () => {
    const s = setup();
    const b = startBattle(s, 'cao', { from: 'xuchang', to: 'runan', officers: ['caocao', 'xuchu', 'guojia'], troops: 35000 });
    autoResolve(s, b);
    expect(b.result).toBe('attacker');
    expect(s.cities.runan.faction).toBe('cao');
    const liuLeft = Object.values(s.officers).filter(o => o.faction === 'liu' && o.status === 'serving');
    // 유비군은 도시가 하나뿐이었으므로 멸망한다
    expect(s.factions.liu.alive).toBe(false);
    expect(liuLeft.length).toBe(0);
    expect(s.captives.some(c => c.officer === 'liubei')).toBe(true);
  });
  it('약한 공격군은 패하고 남은 병력이 출발 도시로 돌아온다', () => {
    const s = fresh('liu', 5);
    const before = s.cities.runan.troops;
    const b = startBattle(s, 'liu', { from: 'runan', to: 'xuchang', officers: ['zhangfei'], troops: 2000 });
    autoResolve(s, b);
    expect(b.result).toBe('defender');
    expect(s.cities.xuchang.faction).toBe('cao');
    expect(s.cities.runan.troops).toBeLessThanOrEqual(before);
    expect(s.cities.runan.troops).toBeGreaterThanOrEqual(before - 2000);
  });
  it('병력은 음수가 되지 않고 최대 12합 안에 끝난다', () => {
    for (let seed = 1; seed <= 30; seed++) {
      const s = fresh('cao', seed);
      s.cities.xuchang.troops = 20000;
      const b = startBattle(s, 'cao', { from: 'xuchang', to: 'wan', officers: ['xuchu'], troops: 15000 });
      autoResolve(s, b);
      expect(b.done).toBe(true);
      expect(b.round).toBeLessThanOrEqual(12);
      expect(b.attacker.troops).toBeGreaterThanOrEqual(0);
      expect(b.defender.troops).toBeGreaterThanOrEqual(0);
    }
  });
  it('퇴각을 고르면 즉시 패배로 끝난다', () => {
    const s = setup();
    const b = startBattle(s, 'cao', { from: 'xuchang', to: 'runan', officers: ['xuchu'], troops: 5000 });
    battleRound(s, b, 'retreat');
    expect(b.done).toBe(true);
    expect(b.result).toBe('defender');
  });
  it('모의전 승률 추정은 실제 상태를 바꾸지 않는다', () => {
    const s = setup();
    const b = startBattle(s, 'cao', { from: 'xuchang', to: 'runan', officers: ['xuchu'], troops: 30000 });
    const snap = serialize(s);
    const p = simulateWinRate(s, b, 10);
    expect(p).toBeGreaterThan(0.5);
    expect(serialize(s)).toBe(snap);
  });
  it('병량이 병력의 30%보다 적으면 출진할 수 없다', () => {
    const s = fresh();
    s.cities.xuchang.food = 100;
    expect(canAttack(s, 'cao', { from: 'xuchang', to: 'runan', officers: ['xuchu'], troops: 5000 })).toMatch(/병량/);
  });
});

describe('턴 진행', () => {
  it('달이 넘어가고 12월 다음은 이듬해 1월이다', () => {
    const s = fresh();
    for (let i = 0; i < 12; i++) endTurn(s);
    expect(s.year).toBe(201);
    expect(s.month).toBe(1);
  });
  it('7월에 병량을 거둔다', () => {
    const s = fresh();
    for (let i = 0; i < 6; i++) endTurn(s); // 7월이 됨
    const f = s.cities.xuchang.food;
    endTurn(s); // 7월 처리
    expect(s.cities.xuchang.food).toBeGreaterThan(f);
  });
  it('재해는 한 달 전에 예고되고 다음 달에 일어난다', () => {
    const s = fresh();
    s.pending.push({ kind: 'locust', city: 'xuchang', month: s.year * 12 + s.month }); // 2월 발생
    endTurn(s); // 1월 → 2월
    const food = s.cities.xuchang.food;
    endTurn(s); // 2월 처리
    expect(s.cities.xuchang.food).toBeLessThan(food);
  });
  it('같은 시드면 결과가 같다 (결정론)', () => {
    const run = () => { const s = fresh('sun', 99); for (let i = 0; i < 24; i++) { aiPlaysPlayer(s); endTurn(s); } return serialize(s); };
    expect(run()).toBe(run());
  });
  it('모든 성을 얻으면 통일 엔딩이다', () => {
    const s = fresh();
    for (const c of Object.values(s.cities)) c.faction = 'cao';
    endTurn(s);
    expect(s.over).toBe(true);
    expect(s.winner).toBe('cao');
  });
  it('모든 성을 잃으면 패배다', () => {
    const s = fresh('liu');
    s.cities.runan.faction = 'cao';
    endTurn(s);
    expect(s.over).toBe(true);
    expect(s.winner).toBeNull();
  });
  it('위임 도시는 플레이어 대신 장수가 일한다', () => {
    const s = fresh();
    s.cities.puyang.policy = 'develop';
    const a = s.cities.puyang.agri + s.cities.puyang.commerce;
    endTurn(s);
    expect(s.cities.puyang.agri + s.cities.puyang.commerce).toBeGreaterThan(a);
  });
});

describe('저장', () => {
  it('저장 후 불러오면 같은 상태다', () => {
    const s = fresh();
    endTurn(s);
    const raw = serialize(s);
    expect(serialize(deserialize(raw))).toBe(raw);
  });
  it('되돌리기는 최근 3턴까지만 보관한다', () => {
    const h = new History(3);
    const s = fresh();
    for (let i = 0; i < 5; i++) { h.push(s); endTurn(s); }
    expect(h.size).toBe(3);
    const back = h.pop() as GameState;
    expect(back.month).toBe(5);
  });
  it('잘못된 저장 데이터는 거부한다', () => {
    expect(() => deserialize('{"version":9}')).toThrow();
  });
});

describe('헬퍼', () => {
  it('도시별 장수 목록과 세력 도시 목록이 맞다', () => {
    const s = fresh();
    expect(factionCities(s, 'cao').map(c => c.id).sort()).toEqual(['luoyang', 'puyang', 'xuchang']);
    expect(cityOfficers(s, 'runan', 'liu').length).toBe(6);
  });
});

describe('회귀 테스트', () => {
  it('군주 도시가 함락돼도 다른 성이 남아 있으면 군주는 피신한다 (시뮬레이션에서 발견)', () => {
    const s = newGame('yuan', 11);
    // 원소를 남피와 이어지지 않은 낙양으로 옮기고, 원소군 소유로 만든 뒤 함락시킨다
    s.cities.luoyang.faction = 'yuan';
    for (const o of Object.values(s.officers)) if (o.city === 'luoyang') o.city = 'xuchang';
    s.officers.yuanshao.city = 'luoyang';
    s.cities.ye.faction = 'cao'; // 업을 잃어 낙양과 남피가 끊긴 상태
    for (const o of Object.values(s.officers)) if (o.city === 'ye' && o.faction === 'yuan') o.city = 'nanpi';
    s.cities.luoyang.troops = 1000;
    s.cities.xuchang.troops = 40000; s.cities.xuchang.food = 99999;
    const b = startBattle(s, 'cao', { from: 'xuchang', to: 'luoyang', officers: ['xuchu'], troops: 30000 });
    autoResolve(s, b);
    expect(b.result).toBe('attacker');
    expect(s.officers.yuanshao.status).toBe('serving');
    expect(s.officers.yuanshao.city).toBe('nanpi');
  });
});
