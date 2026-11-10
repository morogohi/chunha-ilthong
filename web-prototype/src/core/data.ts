// 시나리오 「관도의 각오」(200년) 프로토타입용 12성 축소판.
// 능력치는 모두 산정 위원회 확정 전 가안이다 (상세 기획서 「능력치 산정 기준」 참조).
import type { City, Faction, Officer, Relation } from './types';

type O = [
  id: string, name: string, courtesy: string, born: number,
  war: number, int: number, pol: number, cha: number, land: number, naval: number,
  faction: string | null, city: string, loyalty: number, ambition: number, affinity: number, traits: string,
];

const OFFICERS: O[] = [
  // 위 · 조조
  ['caocao', '조조', '맹덕', 155, 72, 94, 94, 96, 92, 58, 'cao', 'xuchang', 100, 90, 25, '패도,시인'],
  ['xiahoudun', '하후돈', '원양', 157, 88, 58, 70, 80, 84, 50, 'cao', 'puyang', 100, 20, 28, '돌파'],
  ['xiahouyuan', '하후연', '묘재', 162, 89, 55, 52, 72, 86, 44, 'cao', 'puyang', 98, 25, 27, '급습'],
  ['xunyu', '순욱', '문약', 163, 20, 94, 97, 88, 50, 30, 'cao', 'xuchang', 92, 30, 40, '내정'],
  ['guojia', '곽가', '봉효', 170, 22, 96, 70, 76, 74, 40, 'cao', 'xuchang', 95, 30, 30, '간파'],
  ['xuchu', '허저', '중강', 170, 92, 30, 22, 60, 80, 40, 'cao', 'xuchang', 100, 10, 26, '호위'],
  ['zhangliao', '장료', '문원', 169, 92, 76, 58, 78, 92, 66, 'cao', 'luoyang', 90, 30, 60, '돌파,위풍'],
  ['xuhuang', '서황', '공명', 169, 86, 66, 50, 70, 84, 50, 'cao', 'luoyang', 92, 25, 50, '수비'],
  ['xunyou', '순유', '공달', 157, 30, 90, 80, 72, 70, 40, 'cao', 'puyang', 92, 25, 40, '계략'],
  // 하북 · 원소
  ['yuanshao', '원소', '본초', 154, 68, 72, 76, 92, 78, 40, 'yuan', 'ye', 100, 88, 75, '명문'],
  ['yanliang', '안량', '', 160, 92, 34, 30, 58, 82, 30, 'yuan', 'puyang_front', 90, 30, 74, '돌파'],
  ['wenchou', '문추', '', 162, 91, 30, 26, 56, 82, 30, 'yuan', 'nanpi', 90, 30, 74, '돌파'],
  ['jushou', '저수', '', 160, 30, 90, 84, 72, 74, 40, 'yuan', 'ye', 88, 20, 70, '간파'],
  ['tianfeng', '전풍', '원호', 158, 26, 92, 80, 60, 66, 36, 'yuan', 'ye', 86, 20, 72, '간파'],
  ['shenpei', '심배', '정남', 160, 50, 74, 80, 64, 70, 40, 'yuan', 'nanpi', 92, 40, 76, '수비'],
  ['zhanghe', '장합', '준예', 167, 88, 70, 56, 66, 88, 50, 'yuan', 'nanpi', 78, 40, 50, '용병'],
  ['gaolan', '고람', '', 165, 80, 50, 40, 56, 76, 40, 'yuan', 'ye', 76, 30, 60, ''],
  // 촉 · 유비
  ['liubei', '유비', '현덕', 161, 74, 76, 78, 98, 76, 50, 'liu', 'runan', 100, 80, 75, '인덕'],
  ['guanyu', '관우', '운장', 162, 97, 78, 64, 94, 94, 80, 'liu', 'runan', 100, 30, 76, '위풍,의리'],
  ['zhangfei', '장비', '익덕', 165, 95, 40, 30, 50, 90, 50, 'liu', 'runan', 100, 20, 76, '돌파,호통'],
  ['zhaoyun', '조운', '자룡', 168, 93, 74, 64, 80, 88, 52, 'liu', 'runan', 95, 20, 76, '수호'],
  ['sunqian', '손건', '공우', 160, 30, 70, 74, 80, 40, 30, 'liu', 'runan', 95, 15, 76, '외교'],
  ['mizhu', '미축', '자중', 165, 26, 64, 82, 76, 30, 30, 'liu', 'runan', 95, 15, 76, '상인'],
  // 오 · 손권
  ['sunquan', '손권', '중모', 182, 66, 82, 86, 94, 70, 84, 'sun', 'jianye', 100, 85, 120, '수성'],
  ['zhouyu', '주유', '공근', 175, 72, 93, 84, 92, 86, 96, 'sun', 'chaisang', 96, 30, 118, '수군,화공'],
  ['zhangzhao', '장소', '자포', 156, 20, 82, 92, 80, 30, 50, 'sun', 'jianye', 90, 20, 120, '내정'],
  ['chengpu', '정보', '덕모', 150, 80, 70, 60, 72, 78, 84, 'sun', 'chaisang', 92, 20, 118, '수군'],
  ['huanggai', '황개', '공복', 150, 82, 64, 56, 70, 76, 86, 'sun', 'chaisang', 98, 15, 118, '화공'],
  ['taishici', '태사자', '자의', 166, 90, 64, 46, 76, 84, 72, 'sun', 'jianye', 92, 25, 115, '궁술'],
  ['lusu', '노숙', '자경', 172, 50, 88, 86, 84, 70, 74, 'sun', 'jianye', 94, 20, 116, '외교'],
  // 형주 · 유표
  ['liubiao', '유표', '경승', 142, 40, 70, 80, 84, 60, 70, 'biao', 'xiangyang', 100, 40, 90, '명사'],
  ['caimao', '채모', '덕규', 165, 66, 70, 64, 50, 60, 82, 'biao', 'xiangyang', 70, 60, 95, '수군'],
  ['huangzu', '황조', '', 150, 70, 46, 40, 44, 62, 76, 'biao', 'wan', 78, 40, 92, '수군'],
  ['kuailiang', '괴량', '자유', 160, 26, 84, 82, 74, 40, 40, 'biao', 'xiangyang', 80, 30, 88, '계략'],
  ['wenpin', '문빙', '중업', 165, 82, 66, 60, 70, 80, 70, 'biao', 'wan', 85, 25, 85, '수비'],
  // 서량 · 마등
  ['mateng', '마등', '수성', 156, 84, 50, 58, 80, 84, 30, 'ma', 'changan', 100, 60, 20, '기병'],
  ['machao', '마초', '맹기', 176, 94, 44, 30, 82, 92, 30, 'ma', 'changan', 96, 70, 20, '기병,돌파'],
  ['hansui', '한수', '문약', 145, 72, 74, 66, 70, 76, 30, 'ma', 'changan', 70, 80, 30, '이간'],
  ['pangde', '방덕', '영명', 170, 90, 60, 40, 66, 86, 50, 'ma', 'changan', 92, 20, 22, '결사'],
  // 재야 · 아직 섬기는 주군이 없는 인재
  ['zhugeliang', '제갈량', '공명', 181, 38, 98, 96, 92, 80, 60, null, 'xiangyang', 0, 40, 76, '신산,내정'],
  ['pangtong', '방통', '사원', 179, 36, 94, 82, 64, 82, 50, null, 'chaisang', 0, 40, 80, '계략'],
  ['xushu', '서서', '원직', 172, 64, 88, 74, 78, 80, 40, null, 'wan', 0, 30, 76, '간파'],
  ['ganning', '감녕', '흥패', 170, 92, 70, 40, 62, 82, 90, null, 'chaisang', 0, 60, 110, '수군,야습'],
  ['luxun', '육손', '백언', 183, 64, 94, 88, 84, 84, 90, null, 'jianye', 0, 30, 118, '화공'],
  ['simayi', '사마의', '중달', 179, 60, 96, 92, 80, 92, 60, null, 'luoyang', 0, 95, 30, '간파,인내'],
  ['jiaxu', '가후', '문화', 147, 40, 94, 80, 60, 80, 40, null, 'wan', 0, 50, 35, '계략'],
  ['liuye', '유엽', '자양', 175, 30, 88, 80, 66, 60, 50, null, 'shouchun', 0, 30, 40, '계략'],
];

export function makeOfficers(): Record<string, Officer> {
  const out: Record<string, Officer> = {};
  for (const r of OFFICERS) {
    const [id, name, courtesy, born, war, int, pol, cha, land, naval, faction, city, loyalty, ambition, affinity, traits] = r;
    out[id] = {
      id, name, courtesy, born,
      stats: { war, int, pol, cha, land, naval },
      loyalty, ambition, affinity,
      traits: traits ? traits.split(',') : [],
      faction,
      city: city === 'puyang_front' ? 'puyang' : city,
      status: faction ? 'serving' : 'free',
      acted: false,
      found: false,
    };
  }
  // 안량은 원소군 전방(업)에 배치
  out.yanliang.city = 'ye';
  out.zhugeliang.special = 'thrice_visit';
  out.zhugeliang.visits = 0;
  return out;
}

type C = [id: string, name: string, x: number, y: number, faction: string | null, gov: string | null,
  agri: number, commerce: number, flood: number, loyalty: number, wall: number, pop: number,
  troops: number, gold: number, food: number, river: boolean];

const CITIES: C[] = [
  ['ye', '업', 250, 60, 'yuan', 'yuanshao', 520, 480, 50, 70, 80, 120, 32000, 1600, 42000, false],
  ['nanpi', '남피', 330, 40, 'yuan', 'shenpei', 420, 360, 40, 64, 60, 90, 18000, 900, 26000, false],
  ['puyang', '복양', 300, 125, 'cao', 'xiahoudun', 380, 340, 45, 62, 60, 80, 16000, 800, 20000, true],
  ['luoyang', '낙양', 175, 145, 'cao', 'zhangliao', 300, 420, 50, 58, 85, 70, 14000, 900, 16000, true],
  ['xuchang', '허창', 250, 200, 'cao', 'caocao', 480, 500, 55, 76, 75, 110, 26000, 1400, 32000, false],
  ['changan', '장안', 60, 150, 'ma', 'mateng', 360, 380, 40, 60, 90, 90, 22000, 1000, 24000, false],
  ['wan', '완', 165, 245, 'biao', 'wenpin', 340, 300, 40, 60, 55, 70, 12000, 700, 16000, false],
  ['runan', '여남', 285, 275, 'liu', 'liubei', 300, 260, 35, 72, 45, 60, 9000, 500, 12000, false],
  ['shouchun', '수춘', 340, 320, null, null, 380, 340, 40, 50, 55, 70, 3000, 400, 10000, true],
  ['xiangyang', '양양', 160, 340, 'biao', 'liubiao', 500, 520, 60, 74, 85, 100, 24000, 1500, 30000, true],
  ['chaisang', '시상', 250, 420, 'sun', 'zhouyu', 360, 380, 55, 66, 65, 70, 15000, 800, 18000, true],
  ['jianye', '건업', 350, 400, 'sun', 'sunquan', 460, 500, 55, 74, 80, 100, 22000, 1300, 28000, true],
];

export function makeCities(): Record<string, City> {
  const out: Record<string, City> = {};
  for (const r of CITIES) {
    const [id, name, x, y, faction, governor, agri, commerce, flood, loyalty, wall, pop, troops, gold, food, river] = r;
    out[id] = { id, name, x, y, faction, governor, agri, commerce, flood, loyalty, wall, pop, troops, training: 50, gold, food, river, policy: 'balanced' };
  }
  return out;
}

export const ROADS: [string, string][] = [
  ['ye', 'nanpi'], ['ye', 'puyang'], ['ye', 'luoyang'], ['nanpi', 'puyang'],
  ['puyang', 'xuchang'], ['xuchang', 'luoyang'], ['xuchang', 'runan'], ['xuchang', 'wan'],
  ['luoyang', 'changan'], ['luoyang', 'wan'], ['changan', 'wan'],
  ['runan', 'shouchun'], ['runan', 'wan'], ['wan', 'xiangyang'],
  ['xiangyang', 'chaisang'], ['shouchun', 'jianye'], ['jianye', 'chaisang'], ['shouchun', 'chaisang'],
];

export function makeFactions(): Record<string, Faction> {
  const f = (id: string, name: string, ruler: string, color: string, mandate: number): Faction =>
    ({ id, name, ruler, color, mandate, alive: true });
  return {
    cao: f('cao', '조조군', 'caocao', '#B08A3E', 60),
    yuan: f('yuan', '원소군', 'yuanshao', '#6B5B8E', 65),
    liu: f('liu', '유비군', 'liubei', '#B5432F', 55),
    sun: f('sun', '손권군', 'sunquan', '#2F5D7C', 55),
    biao: f('biao', '유표군', 'liubiao', '#5E7D4A', 50),
    ma: f('ma', '마등군', 'mateng', '#4A4A48', 45),
  };
}

export function makeRelations(ids: string[]): Relation[] {
  const rel: Relation[] = [];
  for (let i = 0; i < ids.length; i++)
    for (let j = i + 1; j < ids.length; j++) rel.push({ a: ids[i], b: ids[j], alliance: 0, truce: 0, favor: 40 });
  const set = (a: string, b: string, favor: number, alliance = 0) => {
    const r = rel.find(x => (x.a === a && x.b === b) || (x.a === b && x.b === a))!;
    r.favor = favor; r.alliance = alliance;
  };
  set('cao', 'yuan', 10);
  set('liu', 'cao', 15);
  set('liu', 'yuan', 60);
  set('liu', 'biao', 55);
  set('sun', 'biao', 15);
  return rel;
}

export const SCENARIO = { year: 200, month: 1, title: '관도의 각오' };

export const FACTION_ORDER = ['cao', 'yuan', 'liu', 'sun', 'biao', 'ma'];
