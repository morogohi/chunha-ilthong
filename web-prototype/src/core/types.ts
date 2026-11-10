export type FactionId = string;
export type CityId = string;
export type OfficerId = string;

export interface Stats {
  war: number; // 무력
  int: number; // 지력
  pol: number; // 정치
  cha: number; // 매력
  land: number; // 육전
  naval: number; // 수전
}

export type OfficerStatus = 'serving' | 'free' | 'captive' | 'dead' | 'hidden';

export interface Officer {
  id: OfficerId;
  name: string;
  courtesy: string;
  born: number;
  stats: Stats;
  loyalty: number; // 0-100
  ambition: number; // 0-100 hidden
  affinity: number; // 0-149
  traits: string[];
  faction: FactionId | null;
  city: CityId;
  status: OfficerStatus;
  acted: boolean; // 이번 달 행동 완료
  found: boolean; // 재야: 탐색으로 발견됨
  special?: 'thrice_visit';
  visits?: number;
}

export type Policy = 'direct' | 'develop' | 'military' | 'welfare' | 'balanced';

export interface City {
  id: CityId;
  name: string;
  x: number;
  y: number;
  faction: FactionId | null;
  governor: OfficerId | null;
  agri: number; // 0-999
  commerce: number; // 0-999
  flood: number; // 0-100
  loyalty: number; // 0-100
  wall: number; // 0-100
  pop: number; // 만 명 단위 x100
  troops: number;
  training: number; // 0-100
  gold: number;
  food: number;
  river: boolean; // 수상 도시
  policy: Policy;
}

export interface Faction {
  id: FactionId;
  name: string;
  ruler: OfficerId;
  color: string;
  mandate: number; // 천명 0-100
  alive: boolean;
}

export interface Relation {
  a: FactionId;
  b: FactionId;
  alliance: number; // 남은 개월, 0 = 없음
  truce: number;
  favor: number; // 0-100
}

export interface PendingEvent {
  kind: 'locust' | 'flood' | 'plague';
  city: CityId;
  month: number; // 발생 예정 (절대 월)
}

export interface LogEntry {
  t: number;
  text: string;
  tone?: 'good' | 'bad' | 'info';
}

export interface Captive {
  officer: OfficerId;
  by: FactionId;
}

export interface GameState {
  version: 1;
  seed: number;
  rng: number;
  year: number;
  month: number; // 1-12
  player: FactionId;
  cities: Record<CityId, City>;
  officers: Record<OfficerId, Officer>;
  factions: Record<FactionId, Faction>;
  relations: Relation[];
  roads: [CityId, CityId][];
  pending: PendingEvent[];
  log: LogEntry[];
  report: string[]; // 이번 달 사관 보고
  captives: Captive[];
  chronicle: string[]; // 나만의 삼국사
  winner: FactionId | null;
  over: boolean;
}

export type Tactic = 'charge' | 'fire' | 'defend' | 'retreat';

export interface Side {
  faction: FactionId;
  officers: OfficerId[];
  troops: number;
  morale: number; // 0-100
  food: number;
  start?: number; // 출진 시점 병력 (화면 표시용)
}

export interface Battle {
  attacker: Side;
  defender: Side;
  city: CityId;
  from: CityId;
  round: number;
  maxRounds: number;
  log: string[];
  done: boolean;
  result: 'attacker' | 'defender' | null;
}
