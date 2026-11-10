import './style.css';
import {
  newGame, endTurn, domestic, domesticEffect, COMMAND_INFO, search, recruit, recruitChance, releaseCaptive,
  moveOfficer, transport, setPolicy, proposeAlliance, allianceChance, sendGift, sow, schemeChance, rumor,
  startBattle, battleRound, autoResolve, canAttack, simulateWinRate, TACTIC_LABEL,
  cityOfficers, factionCities, freeOfficers, neighbors, isHostile, relation, factionName, cityName, isRuler,
  saveLocal, loadLocal, clearLocal, saveBattle, loadBattle, History, FACTION_ORDER, SCENARIO, GRACE_MONTHS,
} from '../core';
import type { Battle, DomesticCmd, GameState, Officer, Policy, Tactic } from '../core';

const app = document.getElementById('app')!;
let s: GameState | null = null;
let selected = '';
let modal: string | null = null; // 모달 HTML
let battle: Battle | null = null;
let attackPlan: { from: string; to: string; officers: string[]; troops: number } | null = null;
let tab: 'domestic' | 'people' | 'diplomacy' | 'scheme' = 'domestic';
let transportPlan: { from: string; to: string; officer: string; troops: number; gold: number; food: number } | null = null;
let pendingStart: string | null = null; // 저장이 있을 때 새 군주를 고르면 먼저 확인한다
let toastTimer = 0;
const history = new History(3, true);

const esc = (t: string | number) => String(t).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
const fmt = (n: number) => Math.round(n).toLocaleString('ko-KR');
const DIFF: Record<string, string> = { cao: '보통', yuan: '쉬움', liu: '어려움', sun: '보통', biao: '보통', ma: '어려움' };
const POLICY: Record<Policy, string> = { direct: '직할', develop: '개발', military: '군비', welfare: '진흥', balanced: '균형' };

function toast(msg: string) {
  document.querySelector('.toast')?.remove();
  const el = document.createElement('div');
  el.className = 'toast';
  el.setAttribute('role', 'status');
  el.textContent = msg;
  document.body.appendChild(el);
  clearTimeout(toastTimer);
  toastTimer = window.setTimeout(() => el.remove(), 2200);
}

function persist() { if (s) { saveLocal(s); saveBattle(battle && s ? battle : null); } }

// ───────────────────────── 화면: 타이틀
function titleView(): string {
  const saved = loadLocal();
  const preview = newGame('cao', 1);
  const items = FACTION_ORDER.map(id => {
    const f = preview.factions[id];
    const cs = factionCities(preview, id);
    const ruler = preview.officers[f.ruler];
    const troops = cs.reduce((a, c) => a + c.troops, 0);
    return `<button class="faction-btn" data-act="start" data-f="${id}">
      <span class="sw" style="background:${f.color}"></span>
      <span><strong>${esc(ruler.name)}</strong><br><span class="meta">${esc(f.name)} · ${cs.length}성 · 병력 ${fmt(troops)}</span></span>
      <span class="diff">${DIFF[id]}</span></button>`;
  }).join('');
  return `<main class="title">
    <div class="historian"><span class="seal" aria-hidden="true">許</span><span class="muted small">기록관 허문이 이 시대를 적는다</span></div>
    <h1>천하일통</h1>
    <p class="lead">${SCENARIO.year}년, 시나리오 「${SCENARIO.title}」. 12개의 성을 두고 여섯 군웅이 맞선다. 한 달에 한 번, 군주로서 명령을 내려 천하를 하나로 묶어라.</p>
    <ol class="howto small" aria-label="진행 방법"><li>지도에서 <b>도시</b>를 고른다</li><li>장수마다 <b>한 달에 한 번</b> 명령한다</li><li><b>턴 종료</b>로 다음 달로 넘긴다</li></ol>
    ${saved && !saved.over ? `<button class="act primary" data-act="continue">이어하기 — ${saved.year}년 ${saved.month}월 ${esc(factionName(saved, saved.player))}</button>` : ''}
    <h2 style="font-size:18px">군주를 고른다</h2>
    <div class="faction-list">${items}</div>
    ${pendingStart && saved && !saved.over ? confirmOverwrite(saved, pendingStart) : ''}
    <p class="muted small">브라우저 체험판 · 12성 축소판 · 개전 후 ${GRACE_MONTHS}개월은 AI가 플레이어를 공격하지 않는다</p>
  </main>`;
}

function confirmOverwrite(saved: GameState, f: string): string {
  const preview = newGame(f, 1);
  const name = preview.officers[preview.factions[f].ruler].name;
  return `<div class="modal-bg"><div class="modal" role="alertdialog" aria-modal="true" aria-labelledby="ow-title" aria-describedby="ow-desc">
    <h2 id="ow-title">새로 시작할까요?</h2>
    <p id="ow-desc" style="margin:0">저장 칸은 하나다. <b>${esc(name)}</b>(으)로 시작하면 진행 중인 기록
      <b>${saved.year}년 ${saved.month}월 ${esc(factionName(saved, saved.player))}</b>이(가) 지워진다.</p>
    <div class="tactics"><button class="act" data-act="start-cancel">취소</button>
      <button class="act primary" data-act="start-confirm" data-f="${f}">지우고 새로 시작</button></div></div></div>`;
}

// ───────────────────────── 화면: 메인
function mapView(st: GameState): string {
  const roads = st.roads.map(([a, b]) => {
    const A = st.cities[a], B = st.cities[b];
    return `<line class="road" x1="${A.x + 20}" y1="${A.y + 20}" x2="${B.x + 20}" y2="${B.y + 20}"/>`;
  }).join('');
  const pend = new Set(st.pending.map(p => p.city));
  const cities = Object.values(st.cities).map(c => {
    const col = c.faction ? st.factions[c.faction].color : '#B9B1A1';
    const sel = c.id === selected;
    const mine = c.faction === st.player;
    return `<g class="city-hit" data-act="select" data-c="${c.id}" role="button" tabindex="0" aria-label="${esc(c.name)} ${esc(factionName(st, c.faction))} 병력 ${fmt(c.troops)}">
      ${sel ? `<circle cx="${c.x + 20}" cy="${c.y + 20}" r="24" fill="none" stroke="#1E1C1A" stroke-width="2.5"/>` : ''}
      <circle cx="${c.x + 20}" cy="${c.y + 20}" r="17" fill="${col}" stroke="${mine ? '#1E1C1A' : '#F6F1E6'}" stroke-width="${mine ? 3 : 2}"/>
      <text x="${c.x + 20}" y="${c.y + 25}" text-anchor="middle" font-size="13" fill="#F6F1E6">${esc(c.name)}</text>
      <text x="${c.x + 20}" y="${c.y + 52}" text-anchor="middle" font-size="11" fill="#45413B">${fmt(c.troops / 1000)}k</text>
      ${pend.has(c.id) ? `<text x="${c.x + 38}" y="${c.y + 6}" font-size="14" fill="#B5432F">!</text>` : ''}
      <circle cx="${c.x + 20}" cy="${c.y + 20}" r="26" fill="transparent"/>
    </g>`;
  }).join('');
  return `<div class="map-wrap"><svg class="map" viewBox="0 0 400 470" role="group" aria-label="천하 지도">
    <path d="M20 300 Q120 280 210 330 T400 380" fill="none" stroke="#9FB3BF" stroke-width="6" opacity="0.5"/>
    <path d="M10 120 Q120 100 230 150 T400 140" fill="none" stroke="#C9B27C" stroke-width="6" opacity="0.45"/>
    ${roads}${cities}</svg></div>`;
}

function bar(label: string, v: number, max: number) {
  return `<div class="stat"><span class="lbl">${label}<b>${fmt(v)}</b></span><span class="bar"><span style="width:${Math.min(100, (v / max) * 100)}%"></span></span></div>`;
}

function officerRow(st: GameState, o: Officer) {
  const t = o.stats;
  return `<div class="off ${o.acted ? 'acted' : ''}"><span class="nm">${esc(o.name)}${isRuler(st, o) ? ' · 군주' : st.cities[o.city].governor === o.id ? ' · 태수' : ''}</span>
    <span class="small">${o.acted ? '행동 완료' : '대기'}</span>
    <span class="st">무 ${t.war} · 지 ${t.int} · 정 ${t.pol} · 매 ${t.cha} · 육 ${t.land} · 수 ${t.naval}${isRuler(st, o) ? '' : ` · 충성 ${o.loyalty}`}</span></div>`;
}

function cityCard(st: GameState): string {
  const c = st.cities[selected];
  if (!c) return '';
  const mine = c.faction === st.player;
  const col = c.faction ? st.factions[c.faction].color : '#B9B1A1';
  const os = c.faction ? cityOfficers(st, c.id, c.faction) : [];
  const gov = c.governor ? st.officers[c.governor] : null;
  const pend = st.pending.find(p => p.city === c.id);
  const canAtkFrom = neighbors(st, c.id).filter(n => st.cities[n].faction === st.player);
  const hostile = !mine && isHostile(st, st.player, c.faction);
  const idle = os.filter(o => !o.acted).length;
  return `<section class="card" aria-label="${esc(c.name)} 정보">
    <div class="card-head"><h2>${esc(c.name)}</h2><span class="chip"><span class="dot" style="background:${col}"></span>${esc(factionName(st, c.faction))}</span>
      <span class="muted small">${gov ? `태수 ${esc(gov.name)}` : '태수 없음'}${c.river ? ' · 수상 도시' : ''}</span></div>
    ${pend && mine ? `<p class="small" style="margin:0;color:#B5432F">다음 달 재해 조짐: ${pend.kind === 'flood' ? '홍수' : pend.kind === 'locust' ? '메뚜기' : '역병'}</p>` : ''}
    <div class="nums"><div>병력<b>${fmt(c.troops)}</b></div><div>훈련<b>${c.training}</b></div><div>금<b>${mine ? fmt(c.gold) : '?'}</b></div><div>병량<b>${mine ? fmt(c.food) : '?'}</b></div></div>
    <div class="stats">${bar('농업', c.agri, 999)}${bar('상업', c.commerce, 999)}${bar('치수', c.flood, 100)}${bar('민심', c.loyalty, 100)}${bar('성벽', c.wall, 100)}${bar('인구', c.pop, 200)}</div>
    ${mine ? `<div><p class="small muted" style="margin:0 0 6px">운영 방침 — 직할이 아니면 남은 장수가 턴 종료 때 알아서 일한다</p>
      <div class="policy">${(Object.keys(POLICY) as Policy[]).map(p => `<button class="pill" data-act="policy" data-p="${p}" aria-pressed="${c.policy === p}">${POLICY[p]}</button>`).join('')}</div></div>` : ''}
    ${mine ? `<div class="actions">
      <button class="act" data-act="cmd" data-tab="domestic" ${idle ? '' : 'disabled'}>내정</button>
      <button class="act" data-act="cmd" data-tab="people" ${idle ? '' : 'disabled'}>인사</button>
      <button class="act" data-act="cmd" data-tab="diplomacy" ${idle ? '' : 'disabled'}>외교</button>
      <button class="act" data-act="cmd" data-tab="scheme" ${idle ? '' : 'disabled'}>계략</button>
      <button class="act primary" data-act="attack-from" ${idle ? '' : 'disabled'}>출진</button>
      <button class="act" data-act="transport" ${idle ? '' : 'disabled'}>수송</button>
    </div>` : hostile && canAtkFrom.length ? `<button class="act primary" data-act="attack-to">${esc(c.name)} 공격 준비</button>` : ''}
    <div class="officers">${os.length ? os.map(o => officerRow(st, o)).join('') : '<p class="muted small" style="margin:0">주둔 장수 없음</p>'}</div>
    ${mine && freeOfficers(st, c.id).some(o => o.found) ? `<p class="small" style="margin:0">재야: ${freeOfficers(st, c.id).filter(o => o.found).map(o => esc(o.name)).join(', ')}</p>` : ''}
  </section>`;
}

function mainView(st: GameState): string {
  const f = st.factions[st.player];
  const cs = factionCities(st, st.player);
  const gold = cs.reduce((a, c) => a + c.gold, 0), food = cs.reduce((a, c) => a + c.food, 0), troops = cs.reduce((a, c) => a + c.troops, 0);
  return `<header class="topbar"><div class="row"><span class="date">${st.year}년 ${st.month}월</span>
      <span class="chip"><span class="dot" style="background:${f.color}"></span>${esc(f.name)} · 천명 ${Math.round(f.mandate)}</span></div>
    <div class="res"><span>${cs.length}성</span><span>병력 ${fmt(troops)}</span><span>금 ${fmt(gold)}</span><span>병량 ${fmt(food)}</span></div></header>
    ${mapView(st)}
    <div class="sheet">${cityCard(st)}</div>
    <nav class="bottombar" aria-label="턴"><div class="inner">
      <button class="act" data-act="log">기록</button>
      <button class="act" data-act="undo" ${history.size ? '' : 'disabled'} title="${history.size ? esc(history.nextLabel + '으로 되돌린다') : '되돌릴 기록 없음'}">되돌리기 ${history.size}</button>
      <button class="act end" data-act="end">턴 종료</button></div></nav>`;
}

// ───────────────────────── 모달
function wrapModal(title: string, body: string, closable = true) {
  return `<div class="modal-bg" data-act="${closable ? 'close-bg' : ''}"><div class="modal" role="dialog" aria-modal="true" aria-label="${esc(title)}" tabindex="-1">
    ${closable ? '<button class="close" data-act="close">닫기</button>' : ''}<h2>${esc(title)}</h2>${body}</div></div>`;
}

function cmdModal(st: GameState): string {
  const c = st.cities[selected];
  const idle = cityOfficers(st, c.id, st.player).filter(o => !o.acted);
  const tabs = ([['domestic', '내정'], ['people', '인사'], ['diplomacy', '외교'], ['scheme', '계략']] as const)
    .map(([k, l]) => `<button class="pill" data-act="tab" data-tab="${k}" aria-pressed="${tab === k}">${l}</button>`).join('');
  let body = '';
  if (!idle.length) body = '<p class="muted">이번 달 움직일 장수가 없다.</p>';
  else if (tab === 'domestic') {
    body = (Object.keys(COMMAND_INFO) as DomesticCmd[]).map(cmd => {
      const info = COMMAND_INFO[cmd];
      const bestO = idle.slice().sort((a, b) => b.stats[info.stat] - a.stats[info.stat])[0];
      const eff = domesticEffect(bestO, cmd);
      return `<button class="choice" data-act="dom" data-cmd="${cmd}" data-o="${bestO.id}"><span>${info.label} <small>${info.cost} · ${esc(bestO.name)}</small></span><span class="eff">+${fmt(eff)}</span></button>`;
    }).join('');
    body += '<p class="small muted">해당 능력치가 가장 높은 대기 장수가 맡는다.</p>';
  } else if (tab === 'people') {
    const found = freeOfficers(st, c.id).filter(o => o.found && o.special !== 'thrice_visit');
    const ruler = idle.find(o => isRuler(st, o));
    const searcher = ruler ?? idle.slice().sort((a, b) => b.stats.int + b.stats.cha - a.stats.int - a.stats.cha)[0];
    body = `<button class="choice" data-act="search" data-o="${searcher.id}"><span>인재 탐색 <small>${esc(searcher.name)}${ruler ? ' (군주 직접)' : ''}</small></span><span class="eff">탐색</span></button>`;
    const recruiter = idle.slice().sort((a, b) => b.stats.cha - a.stats.cha)[0];
    body += found.map(t => `<button class="choice" data-act="recruit" data-o="${recruiter.id}" data-t="${t.id}"><span>등용: ${esc(t.name)} <small>지 ${t.stats.int} 무 ${t.stats.war} · ${esc(recruiter.name)}</small></span><span class="eff">${Math.round(recruitChance(st, recruiter, t, st.player) * 100)}%</span></button>`).join('');
    const dests = neighbors(st, c.id).filter(n => st.cities[n].faction === st.player);
    body += idle.filter(o => !isRuler(st, o) || dests.length).flatMap(o => dests.map(d =>
      `<button class="choice" data-act="move" data-o="${o.id}" data-to="${d}"><span>이동: ${esc(o.name)} → ${esc(cityName(st, d))}</span><span class="eff">이동</span></button>`)).join('');
  } else if (tab === 'diplomacy') {
    const envoy = idle.slice().sort((a, b) => b.stats.cha - a.stats.cha)[0];
    body = Object.values(st.factions).filter(f => f.alive && f.id !== st.player).map(f => {
      const r = relation(st, st.player, f.id)!;
      return `<div class="card" style="gap:6px"><div class="card-head"><span class="chip"><span class="dot" style="background:${f.color}"></span><b>${esc(f.name)}</b></span>
        <span class="small muted">우호 ${r.favor}${r.alliance > 0 ? ` · 동맹 ${r.alliance}개월` : ''}</span></div>
        <button class="choice" data-act="ally" data-o="${envoy.id}" data-f="${f.id}" ${r.alliance > 0 ? 'disabled' : ''}><span>동맹 제안 <small>금 300 · ${esc(envoy.name)}</small></span><span class="eff">${Math.round(allianceChance(st, envoy, st.player, f.id) * 100)}%</span></button>
        <button class="choice" data-act="gift" data-o="${envoy.id}" data-f="${f.id}"><span>예물 <small>금 300 · 우호 상승</small></span><span class="eff">+${5 + Math.round(envoy.stats.cha / 10)}</span></button></div>`;
    }).join('');
  } else {
    const plotter = idle.slice().sort((a, b) => b.stats.int - a.stats.int)[0];
    const adj = neighbors(st, c.id).map(n => st.cities[n]).filter(n => n.faction && isHostile(st, st.player, n.faction));
    body = adj.map(n => {
      const targets = cityOfficers(st, n.id, n.faction).filter(o => !isRuler(st, o));
      return `<h3 style="font-size:16px">${esc(n.name)} <span class="small muted">${esc(factionName(st, n.faction))}</span></h3>
        <button class="choice" data-act="rumor" data-o="${plotter.id}" data-to="${n.id}"><span>유언비어 <small>민심 하락 · ${esc(plotter.name)}</small></span><span class="eff">민심 ${n.loyalty}</span></button>
        ${targets.map(t => `<button class="choice" data-act="sow" data-o="${plotter.id}" data-t="${t.id}"><span>이간: ${esc(t.name)} <small>충성 하락</small></span><span class="eff">${Math.round(schemeChance(plotter, t) * 100)}%</span></button>`).join('')}`;
    }).join('') || '<p class="muted">인접한 적 도시가 없다.</p>';
  }
  return wrapModal(`${c.name} · 명령`, `<div class="tabs">${tabs}</div><div class="list">${body}</div>`);
}

function attackSetupModal(st: GameState): string {
  const p = attackPlan!;
  const from = st.cities[p.from];
  const targets = neighbors(st, from.id).filter(n => st.cities[n].faction !== st.player && isHostile(st, st.player, st.cities[n].faction));
  const os = cityOfficers(st, from.id, st.player).filter(o => !o.acted);
  const err = canAttack(st, st.player, p);
  let rate = '';
  if (!err) {
    const clone: GameState = JSON.parse(JSON.stringify(st));
    const b = startBattle(clone, st.player, p);
    rate = `${Math.round(simulateWinRate(clone, b, 16) * 100)}%`;
  }
  const to = st.cities[p.to];
  return wrapModal(`${from.name}에서 출진`, `
    <div class="list"><p class="small muted" style="margin:0">목표</p><div class="policy">${targets.map(t => `<button class="pill" data-act="atk-target" data-to="${t}" aria-pressed="${p.to === t}">${esc(cityName(st, t))} · ${fmt(st.cities[t].troops)}</button>`).join('') || '<span class="muted">공격할 수 있는 인접 도시가 없다.</span>'}</div>
    <p class="small muted" style="margin:0">장수 (최대 3명)</p>
    ${os.map(o => `<button class="choice" data-act="atk-off" data-o="${o.id}" aria-pressed="${p.officers.includes(o.id)}" style="${p.officers.includes(o.id) ? 'border-color:#1E1C1A;border-width:2px' : ''}"><span>${esc(o.name)} <small>무 ${o.stats.war} · 지 ${o.stats.int} · ${to?.river ? `수 ${o.stats.naval}` : `육 ${o.stats.land}`}</small></span><span class="eff">${p.officers.includes(o.id) ? '선택' : ''}</span></button>`).join('')}
    <label class="small" for="troops">병력 ${fmt(p.troops)} / ${fmt(from.troops)} · 병량 ${fmt(p.troops * 0.3)} 소모</label>
    <input id="troops" class="range" type="range" min="1000" max="${Math.max(1000, from.troops)}" step="500" value="${p.troops}" data-act="atk-troops">
    ${to ? `<p class="small" style="margin:0">${esc(to.name)}: 병력 ${fmt(to.troops)}, 성벽 ${to.wall}${to.river ? ', 수상전' : ''}</p>` : ''}
    ${err ? `<p class="small" style="margin:0;color:#B5432F">${esc(err)}</p>` : `<p class="small" style="margin:0">모의전 예상 승률 <b>${rate}</b></p>`}
    <button class="act primary" data-act="atk-go" ${err ? 'disabled' : ''}>출진</button></div>`);
}

function transportModal(st: GameState): string {
  const p = transportPlan!;
  const c = st.cities[p.from];
  const dests = neighbors(st, c.id).filter(n => st.cities[n].faction === st.player);
  const os = cityOfficers(st, c.id, st.player).filter(o => !o.acted);
  const to = st.cities[p.to];
  const slider = (key: 'troops' | 'gold' | 'food', label: string, max: number, step: number) =>
    `<label class="small" for="tp-${key}">${label} <b data-tp-out="${key}">${fmt(p[key])}</b> / ${fmt(max)}</label>
     <input id="tp-${key}" class="range" type="range" min="0" max="${max}" step="${step}" value="${p[key]}" data-act="tp-amount" data-k="${key}">`;
  const empty = !p.troops && !p.gold && !p.food;
  return wrapModal(`${c.name}에서 수송`, `<div class="list">
    <p class="small muted" style="margin:0">목적지 (인접한 아군 성)</p>
    <div class="policy">${dests.map(d => `<button class="pill" data-act="tp-to" data-to="${d}" aria-pressed="${p.to === d}">${esc(cityName(st, d))} · ${fmt(st.cities[d].troops)}</button>`).join('')}</div>
    <p class="small muted" style="margin:0">맡을 장수 (이번 달 행동을 쓴다)</p>
    <div class="policy">${os.map(o => `<button class="pill" data-act="tp-off" data-o="${o.id}" aria-pressed="${p.officer === o.id}">${esc(o.name)}</button>`).join('')}</div>
    ${slider('troops', '병력', c.troops, 500)}${slider('gold', '금', c.gold, 50)}${slider('food', '병량', c.food, 100)}
    <p class="small summary" style="margin:0" data-tp-summary>${transportSummary(st)}</p>
    <div class="tactics"><button class="act" data-act="close">취소</button>
      <button class="act primary" data-act="tp-go" ${empty || !to ? 'disabled' : ''}>수송 실행</button></div></div>`);
}

function transportSummary(st: GameState): string {
  const p = transportPlan!;
  const o = st.officers[p.officer];
  return `${esc(o?.name ?? '')}이(가) ${esc(cityName(st, p.from))} → ${esc(cityName(st, p.to))}로 병력 ${fmt(p.troops)} · 금 ${fmt(p.gold)} · 병량 ${fmt(p.food)}을 옮긴다.`;
}

function battleModal(st: GameState): string {
  const b = battle!;
  const side = (title: string, sd: Battle['attacker'], col: string) => `<div class="side"><h3>${title}</h3>
    <span class="chip"><span class="dot" style="background:${col}"></span>${esc(factionName(st, sd.faction || null))}</span>
    <span>${sd.officers.map(id => esc(st.officers[id].name)).join(', ') || '장수 없음'}</span>
    ${bar('병력', sd.troops, Math.max(sd.start ?? sd.troops, 1))}${bar('사기', sd.morale, 100)}</div>`;
  const aCol = st.factions[b.attacker.faction].color;
  const dCol = b.defender.faction ? st.factions[b.defender.faction].color : '#B9B1A1';
  const tactics = (['charge', 'fire', 'defend', 'retreat'] as Tactic[]).map(t => `<button class="act" data-act="tactic" data-t="${t}">${TACTIC_LABEL[t]}</button>`).join('');
  const caps = b.done ? st.captives.filter(c => c.by === st.player) : [];
  return wrapModal(`${cityName(st, b.city)} 전투 · ${b.round}/${b.maxRounds}합`, `
    <div class="battle-sides">${side('공격', b.attacker, aCol)}${side('수비', b.defender, dCol)}</div>
    <div class="blog" aria-live="polite">${b.log.slice().reverse().map(l => `<div>${esc(l)}</div>`).join('')}</div>
    ${b.done ? `<p style="margin:0;font-family:var(--serif);font-size:18px">${b.result === 'attacker' ? '승리 — 성을 얻었다' : '패배 — 성을 얻지 못했다'}</p>
      ${caps.map(c => { const o = st.officers[c.officer]; return `<div class="card" style="gap:6px"><b>포로 ${esc(o.name)}</b>
        <div class="tactics"><button class="act" data-act="cap-recruit" data-t="${o.id}">등용 시도</button><button class="act" data-act="cap-free" data-t="${o.id}">해방</button><button class="act" data-act="cap-kill" data-t="${o.id}">처형</button></div></div>`; }).join('')}
      <button class="act primary" data-act="battle-close">돌아가기</button>`
    : `<p class="small muted" style="margin:0">돌격은 피해를 크게 주고 받는다. 화계는 지력이 높을수록 잘 통한다. 방어는 사기를 회복한다.</p>
      <div class="tactics">${tactics}</div><button class="act" data-act="tactic-auto">모의전으로 끝내기</button>`}`, false);
}

function reportModal(st: GameState): string {
  return wrapModal(`${st.year}년 ${st.month}월 보고`, `<div class="report"><div class="historian"><span class="seal" aria-hidden="true">許</span><b>기록관 허문</b></div>
    ${st.report.map(r => `<p style="margin:0">${esc(r)}</p>`).join('')}</div><button class="act primary" data-act="close">명령을 내린다</button>`);
}

function logModal(st: GameState): string {
  const items = st.log.slice(-60).reverse().map(l => `<div class="log-item ${l.tone ?? ''}">${Math.floor(l.t / 12)}년 ${l.t % 12 + 1}월 · ${esc(l.text)}</div>`).join('');
  const chron = st.chronicle.slice().reverse().map(c => `<div class="log-item">${esc(c)}</div>`).join('');
  return wrapModal('기록', `<h3 style="font-size:16px">나만의 삼국사</h3>${chron || '<p class="muted small">아직 적힌 일이 없다.</p>'}<h3 style="font-size:16px">최근 소식</h3>${items || '<p class="muted small">없음</p>'}
    <button class="act" data-act="new-game">처음부터 다시</button>`);
}

function endModal(st: GameState): string {
  const win = st.winner === st.player;
  return wrapModal(win ? '천하일통' : '기록의 끝', `<div class="report"><div class="historian"><span class="seal" aria-hidden="true">許</span><b>기록관 허문</b></div>
    <p style="margin:0">${win ? `${esc(factionName(st, st.player))}이(가) ${st.year}년 ${st.month}월, 천하를 하나로 묶었다.` : `${esc(factionName(st, st.player))}의 기록은 ${st.year}년 ${st.month}월에서 끊긴다.`}</p>
    ${st.chronicle.map(c => `<p style="margin:0" class="small">${esc(c)}</p>`).join('')}</div>
    <button class="act primary" data-act="new-game">새로 시작</button>`, false);
}

// ───────────────────────── 렌더
// ───────────────────────── 초점 관리 (팝업 안에 초점을 가두고, 닫으면 연 버튼으로 돌려준다)
let lastTrigger: string | null = null;
const FOCUSABLE = 'button:not([disabled]), input:not([disabled]), [tabindex="0"]';

function selectorFor(el: Element | null): string | null {
  if (!(el instanceof HTMLElement) || !el.dataset.act) return el instanceof HTMLElement && el.id ? `#${el.id}` : null;
  return Object.entries(el.dataset).map(([k, v]) => `[data-${k.replace(/[A-Z]/g, m => '-' + m.toLowerCase())}="${CSS.escape(v ?? '')}"]`).join('');
}

function openDialog(): HTMLElement | null { return app.querySelector<HTMLElement>('.modal'); }

function render() {
  const keep = selectorFor(document.activeElement);
  app.innerHTML = !s ? titleView() : mainView(s) + (modal ?? '');
  const dlg = openDialog();
  if (!dlg) return;
  const again = keep ? dlg.querySelector<HTMLElement>(keep) : null;
  (again ?? dlg.querySelector<HTMLElement>(FOCUSABLE) ?? dlg).focus({ preventScroll: true });
}

function restoreFocus() {
  const el = lastTrigger ? app.querySelector<HTMLElement>(lastTrigger) : null;
  lastTrigger = null;
  el?.focus({ preventScroll: true });
}

function openModal(kind: 'cmd' | 'attack' | 'transport' | 'battle' | 'report' | 'log' | 'end') {
  if (!s) return;
  if (!modal) lastTrigger = selectorFor(document.activeElement);
  modal = kind === 'cmd' ? cmdModal(s) : kind === 'attack' ? attackSetupModal(s) : kind === 'transport' ? transportModal(s) : kind === 'battle' ? battleModal(s)
    : kind === 'report' ? reportModal(s) : kind === 'log' ? logModal(s) : endModal(s);
  currentModal = kind;
  render();
}
let currentModal: string | null = null;
const refreshModal = () => { if (currentModal) openModal(currentModal as never); else render(); };
const closeModal = () => { modal = null; currentModal = null; transportPlan = null; render(); restoreFocus(); };

function result(r: { ok: boolean; msg: string }) {
  toast(r.msg);
  persist();
  refreshModal();
}

function startGame(st: GameState, resumed = false) {
  s = st;
  pendingStart = null;
  if (!resumed) { history.clear(); battle = null; }
  selected = factionCities(st, st.player).find(c => c.governor === st.factions[st.player].ruler)?.id ?? factionCities(st, st.player)[0].id;
  if (resumed) {
    battle = loadBattle(st);
    if (battle) { selected = battle.city; persist(); return openModal('battle'); }
  }
  persist();
  openModal('report');
}

app.addEventListener('click', e => {
  const el = (e.target as HTMLElement).closest<HTMLElement>('[data-act]');
  if (!el) return;
  const act = el.dataset.act!;
  if (act === 'close-bg' && e.target !== el) return; // 모달 안쪽 클릭은 무시
  const d = el.dataset;
  if (act === 'start') {
    const saved = loadLocal();
    if (saved && !saved.over) { pendingStart = d.f!; return render(); }
    return startGame(newGame(d.f!, (Date.now() % 2147483646) + 1));
  }
  if (act === 'start-cancel') { const f = pendingStart; pendingStart = null; lastTrigger = `[data-act="start"][data-f="${f}"]`; render(); return restoreFocus(); }
  if (act === 'start-confirm') { clearLocal(); history.clear(); return startGame(newGame(d.f!, (Date.now() % 2147483646) + 1)); }
  if (act === 'continue') { const l = loadLocal(); if (l) startGame(l, true); return; }
  if (!s) return;
  const st = s;
  const P = st.player;
  switch (act) {
    case 'select': selected = d.c!; return render();
    case 'close': case 'close-bg': return closeModal();
    case 'policy': return result(setPolicy(st, P, selected, d.p as Policy));
    case 'cmd': tab = d.tab as typeof tab; return openModal('cmd');
    case 'tab': tab = d.tab as typeof tab; return openModal('cmd');
    case 'dom': return result(domestic(st, P, d.o!, d.cmd as DomesticCmd));
    case 'search': return result(search(st, P, d.o!));
    case 'recruit': return result(recruit(st, P, d.o!, d.t!));
    case 'move': { const r = moveOfficer(st, P, d.o!, d.to!); return result(r); }
    case 'ally': return result(proposeAlliance(st, P, d.o!, d.f!));
    case 'gift': return result(sendGift(st, P, d.o!, d.f!));
    case 'sow': return result(sow(st, P, d.o!, d.t!));
    case 'rumor': return result(rumor(st, P, d.o!, d.to!));
    case 'transport': {
      const c = st.cities[selected];
      const dests = neighbors(st, c.id).filter(n => st.cities[n].faction === P);
      const o = cityOfficers(st, c.id, P).filter(x => !x.acted).sort((a, b) => b.stats.pol - a.stats.pol)[0];
      if (!dests.length) return toast('수송할 인접 아군 도시가 없다.');
      if (!o) return toast('이번 달 움직일 장수가 없다.');
      // 기본값은 제안일 뿐, 실행 전에 목적지·장수·수량을 고친다
      const dst = dests.map(n => st.cities[n]).sort((a, b) => a.troops - b.troops)[0];
      transportPlan = { from: c.id, to: dst.id, officer: o.id, troops: Math.floor(c.troops / 2 / 500) * 500, gold: Math.floor(c.gold / 3 / 50) * 50, food: Math.floor(c.food / 3 / 100) * 100 };
      return openModal('transport');
    }
    case 'tp-to': transportPlan!.to = d.to!; return openModal('transport');
    case 'tp-off': transportPlan!.officer = d.o!; return openModal('transport');
    case 'tp-go': {
      const p = transportPlan!; transportPlan = null;
      const r = transport(st, P, p.officer, p.to, p.troops, p.gold, p.food);
      toast(r.msg); persist(); return closeModal();
    }
    case 'attack-from': case 'attack-to': {
      let from = selected, to = '';
      if (act === 'attack-to') { to = selected; from = neighbors(st, selected).filter(n => st.cities[n].faction === P).sort((a, b) => st.cities[b].troops - st.cities[a].troops)[0]; }
      else to = neighbors(st, from).find(n => st.cities[n].faction !== P && isHostile(st, P, st.cities[n].faction)) ?? '';
      const os = cityOfficers(st, from, P).filter(o => !o.acted).sort((a, b) => b.stats.war - a.stats.war).slice(0, 2).map(o => o.id);
      attackPlan = { from, to, officers: os, troops: Math.max(1000, Math.floor(st.cities[from].troops * 0.7 / 500) * 500) };
      return openModal('attack');
    }
    case 'atk-target': attackPlan!.to = d.to!; return openModal('attack');
    case 'atk-off': {
      const p = attackPlan!; const i = p.officers.indexOf(d.o!);
      if (i >= 0) p.officers.splice(i, 1); else if (p.officers.length < 3) p.officers.push(d.o!); else toast('장수는 최대 3명이다.');
      return openModal('attack');
    }
    case 'atk-go': {
      history.push(st, `${cityName(st, attackPlan!.to)} 출진 전`);
      try { battle = startBattle(st, P, attackPlan!); } catch (err) { history.popEntry(); return toast((err as Error).message); }
      persist();
      return openModal('battle');
    }
    case 'tactic': battleRound(st, battle!, d.t as Tactic); persist(); return openModal('battle');
    case 'tactic-auto': autoResolve(st, battle!); persist(); return openModal('battle');
    case 'cap-recruit': {
      const c = st.cities[battle!.city];
      const o = cityOfficers(st, c.id, P).sort((a, b) => b.stats.cha - a.stats.cha)[0];
      if (o) o.acted = false; // 전투 직후 설득은 이번 달 행동으로 치지 않는다
      st.officers[d.t!].city = c.id;
      const r = o ? recruit(st, P, o.id, d.t!) : { ok: false, msg: '설득할 장수가 없다.' };
      if (o) o.acted = true;
      return result(r);
    }
    case 'cap-free': return result(releaseCaptive(st, P, d.t!, false));
    case 'cap-kill': return result(releaseCaptive(st, P, d.t!, true));
    case 'battle-close': battle = null; persist(); closeModal(); if (st.over) openModal('end'); return;
    case 'log': return openModal('log');
    case 'undo': {
      const prev = history.popEntry();
      if (prev) { s = prev.state; battle = null; persist(); toast(`${prev.label}으로 되돌렸다.`); closeModal(); }
      return;
    }
    case 'end': {
      history.push(st, `${st.year}년 ${st.month}월 턴 종료 전`);
      endTurn(st);
      persist();
      if (st.over) return openModal('end');
      return openModal('report');
    }
    case 'new-game': clearLocal(); history.clear(); s = null; modal = null; currentModal = null; battle = null; return render();
  }
});

app.addEventListener('input', e => {
  const el = e.target as HTMLInputElement;
  if (el.dataset.act === 'tp-amount' && transportPlan && s) {
    const k = el.dataset.k as 'troops' | 'gold' | 'food';
    transportPlan[k] = Number(el.value);
    const out = document.querySelector(`[data-tp-out="${k}"]`); if (out) out.textContent = fmt(transportPlan[k]);
    const sum = document.querySelector('[data-tp-summary]'); if (sum) sum.innerHTML = transportSummary(s);
    const go = document.querySelector<HTMLButtonElement>('[data-act="tp-go"]');
    if (go) go.disabled = !transportPlan.troops && !transportPlan.gold && !transportPlan.food;
  }
  if (el.dataset.act === 'atk-troops' && attackPlan) {
    attackPlan.troops = Number(el.value);
    const lbl = document.querySelector('label[for="troops"]');
    if (lbl && s) lbl.textContent = `병력 ${fmt(attackPlan.troops)} / ${fmt(s.cities[attackPlan.from].troops)} · 병량 ${fmt(attackPlan.troops * 0.3)} 소모`;
  }
});
app.addEventListener('change', e => {
  const el = e.target as HTMLInputElement;
  if (el.dataset.act === 'atk-troops') openModal('attack'); // 손을 뗀 뒤 승률 다시 계산
});
document.addEventListener('keydown', e => {
  const el = e.target as HTMLElement;
  const dlg = openDialog();
  if (dlg && e.key === 'Escape') {
    // 닫기 버튼이 있는 팝업만 Esc로 닫는다 (전투·엔딩은 결정을 내려야 닫힌다)
    const closer = dlg.querySelector<HTMLElement>('[data-act="close"], [data-act="start-cancel"]');
    if (closer) { e.preventDefault(); closer.click(); }
    return;
  }
  if (dlg && e.key === 'Tab') {
    const items = Array.from(dlg.querySelectorAll<HTMLElement>(FOCUSABLE));
    if (!items.length) { e.preventDefault(); return; }
    const first = items[0], last = items[items.length - 1];
    const inside = dlg.contains(document.activeElement);
    if (e.shiftKey && (document.activeElement === first || !inside)) { e.preventDefault(); last.focus(); }
    else if (!e.shiftKey && (document.activeElement === last || !inside)) { e.preventDefault(); first.focus(); }
    return;
  }
  if ((e.key === 'Enter' || e.key === ' ') && el.matches('g[data-act="select"]')) { e.preventDefault(); el.dispatchEvent(new MouseEvent('click', { bubbles: true })); }
});

// 테스트용 훅: E2E에서 시드를 고정할 수 있게 한다
(window as unknown as { __chunha: unknown }).__chunha = {
  start: (f: string, seed: number) => startGame(newGame(f, seed)),
  state: () => s,
  battle: () => battle,
};

render();

