import type { Battle, GameState } from './types';

const KEY = 'chunha-ilthong-save-v1';
const BATTLE_KEY = 'chunha-ilthong-battle-v1';
const HISTORY_KEY = 'chunha-ilthong-history-v1';

export function serialize(s: GameState): string {
  return JSON.stringify(s);
}

export function deserialize(raw: string): GameState {
  const s = JSON.parse(raw) as GameState;
  if (s.version !== 1 || !s.cities || !s.officers) throw new Error('알 수 없는 저장 형식');
  return s;
}

export function saveLocal(s: GameState): boolean {
  try { localStorage.setItem(KEY, serialize(s)); return true; } catch { return false; }
}

export function loadLocal(): GameState | null {
  try { const raw = localStorage.getItem(KEY); return raw ? deserialize(raw) : null; } catch { return null; }
}

export function clearLocal() {
  try { [KEY, BATTLE_KEY, HISTORY_KEY].forEach(k => localStorage.removeItem(k)); } catch { /* 저장소를 쓸 수 없는 환경 */ }
}

/**
 * 진행 중인 전투. 출진하면 병력이 성에서 빠져 전투로 옮겨 가므로,
 * 게임 상태와 함께 저장하지 않으면 새로고침했을 때 병력이 사라진다.
 */
export function saveBattle(b: Battle | null) {
  try { if (b) localStorage.setItem(BATTLE_KEY, JSON.stringify(b)); else localStorage.removeItem(BATTLE_KEY); } catch { /* 무시 */ }
}

export function loadBattle(s: GameState): Battle | null {
  try {
    const raw = localStorage.getItem(BATTLE_KEY);
    if (!raw) return null;
    const b = JSON.parse(raw) as Battle;
    // 저장된 전투가 지금 게임과 맞는지 확인한다 (다른 판의 찌꺼기면 버린다)
    if (!s.cities[b.city] || !s.cities[b.from] || b.attacker.faction !== s.player && b.defender.faction !== s.player) return null;
    return b;
  } catch { return null; }
}

/** 되돌리기 한 칸: 스냅샷과, 무엇 직전으로 돌아가는지 */
export interface HistoryEntry { label: string; state: string }

/** 역사 되돌리기: 최근 3개 스냅샷. 새로고침해도 남도록 localStorage에 함께 둔다. */
export class History {
  private stack: HistoryEntry[] = [];
  constructor(private limit = 3, private persistent = false) {
    if (persistent) {
      try {
        const raw = localStorage.getItem(HISTORY_KEY);
        const arr = raw ? JSON.parse(raw) : [];
        if (Array.isArray(arr)) this.stack = arr.filter(e => e && typeof e.state === 'string').slice(-limit);
      } catch { this.stack = []; }
    }
  }
  push(s: GameState, label = '한 턴 전') {
    this.stack.push({ label, state: serialize(s) });
    if (this.stack.length > this.limit) this.stack.shift();
    this.save();
  }
  pop(): GameState | null { return this.popEntry()?.state ?? null; }
  popEntry(): { label: string; state: GameState } | null {
    const r = this.stack.pop();
    this.save();
    return r ? { label: r.label, state: deserialize(r.state) } : null;
  }
  clear() { this.stack = []; this.save(); }
  get size() { return this.stack.length; }
  get nextLabel() { return this.stack[this.stack.length - 1]?.label ?? ''; }
  private save() {
    if (!this.persistent) return;
    try { localStorage.setItem(HISTORY_KEY, JSON.stringify(this.stack)); } catch { /* 용량 초과 등은 무시: 되돌리기만 줄어든다 */ }
  }
}
