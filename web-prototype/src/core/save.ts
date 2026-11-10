import type { GameState } from './types';

const KEY = 'chunha-ilthong-save-v1';

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
  try { localStorage.removeItem(KEY); } catch { /* 저장소를 쓸 수 없는 환경 */ }
}

/** 역사 되돌리기: 최근 3턴 스냅샷 */
export class History {
  private stack: string[] = [];
  constructor(private limit = 3) {}
  push(s: GameState) { this.stack.push(serialize(s)); if (this.stack.length > this.limit) this.stack.shift(); }
  pop(): GameState | null { const r = this.stack.pop(); return r ? deserialize(r) : null; }
  get size() { return this.stack.length; }
}
