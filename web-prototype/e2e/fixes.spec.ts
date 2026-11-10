// 2026-11-11 실사용 테스트에서 나온 문제의 회귀 테스트
import { test, expect, type Page } from '@playwright/test';

const errors: string[] = [];
test.beforeEach(async ({ page }) => {
  errors.length = 0;
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error' && !m.text().includes('fonts.g')) errors.push(m.text()); });
  await page.route(/fonts\.(googleapis|gstatic)\.com/, r => r.fulfill({ status: 200, contentType: 'text/css', body: '' }));
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());
  await page.reload();
});
test.afterEach(() => expect(errors).toEqual([]));

const closeReport = (page: Page) => page.getByRole('button', { name: '명령을 내린다' }).click();
const startCao = async (page: Page) => {
  await page.evaluate(() => (window as any).__chunha.start('cao', 7));
  await closeReport(page);
  await page.locator('g[data-c="xuchang"]').click();
};
const troopsOf = (page: Page) => page.evaluate(() => {
  const s = (window as any).__chunha.state(), b = (window as any).__chunha.battle();
  return { xuchang: s.cities.xuchang.troops, runan: s.cities.runan.troops, atk: b?.attacker.troops ?? null, def: b?.defender.troops ?? null, round: b?.round ?? null, done: b?.done ?? null };
});

test('전투 중 새로고침해도 전투와 병력이 그대로 돌아온다', async ({ page }) => {
  await startCao(page);
  await page.getByRole('button', { name: '출진' }).first().click();
  await page.locator('[data-act="atk-target"][data-to="runan"]').click();
  await page.getByRole('button', { name: '출진', exact: true }).last().click();
  await page.locator('[data-act="tactic"][data-t="fire"]').click();
  const before = await troopsOf(page);
  expect(before.atk).toBeGreaterThan(0);
  await page.reload();
  await page.getByRole('button', { name: /이어하기/ }).click();
  await expect(page.getByRole('dialog', { name: /여남 전투/ })).toBeVisible();
  const after = await troopsOf(page);
  expect(after).toEqual(before);
  // 끝까지 싸울 수 있고, 끝나면 전투 저장이 지워진다
  await page.getByRole('button', { name: '모의전으로 끝내기' }).click();
  const caps = page.getByRole('button', { name: '해방' });
  while (await caps.count()) await caps.first().click();
  await page.getByRole('button', { name: '돌아가기' }).click();
  expect(await page.evaluate(() => localStorage.getItem('chunha-ilthong-battle-v1'))).toBeNull();
});

test('저장이 있으면 다른 군주로 시작하기 전에 확인한다', async ({ page }) => {
  await page.locator('.faction-btn', { hasText: '조조' }).click();
  await closeReport(page);
  await page.getByRole('button', { name: '턴 종료' }).click();
  await closeReport(page);
  await page.reload();
  await page.locator('.faction-btn', { hasText: '손권' }).click();
  const dlg = page.getByRole('alertdialog', { name: '새로 시작할까요?' });
  await expect(dlg).toContainText('200년 2월 조조군');
  await page.keyboard.press('Escape');
  await expect(dlg).toBeHidden();
  await expect(page.getByRole('button', { name: /이어하기 — 200년 2월 조조군/ })).toBeVisible();
  await expect(page.locator('.faction-btn', { hasText: '손권' })).toBeFocused();
  await page.locator('.faction-btn', { hasText: '손권' }).click();
  await page.getByRole('button', { name: '지우고 새로 시작' }).click();
  await closeReport(page);
  await expect(page.locator('.date')).toHaveText('200년 1월');
  await expect(page.locator('.topbar')).toContainText('손권군');
});

test('수송은 목적지·장수·수량을 고르고 실행해야 옮겨진다', async ({ page }) => {
  await startCao(page);
  const s0 = await page.evaluate(() => JSON.stringify((window as any).__chunha.state().cities));
  await page.getByRole('button', { name: '수송' }).click();
  const dlg = page.getByRole('dialog', { name: '허창에서 수송' });
  await expect(dlg).toBeVisible();
  expect(await page.evaluate(() => JSON.stringify((window as any).__chunha.state().cities))).toBe(s0); // 아직 아무것도 안 옮겨짐
  const dest = await dlg.locator('[data-act="tp-to"]').first().getAttribute('data-to');
  await dlg.locator('[data-act="tp-to"]').first().click();
  await dlg.locator('#tp-troops').fill('2000');
  await dlg.locator('#tp-gold').fill('0');
  await dlg.locator('#tp-food').fill('1000');
  await expect(dlg.locator('[data-tp-summary]')).toContainText('병력 2,000 · 금 0 · 병량 1,000');
  const before = await page.evaluate(d => { const s = (window as any).__chunha.state(); return { x: s.cities.xuchang.troops, d: s.cities[d!].troops }; }, dest);
  await dlg.getByRole('button', { name: '수송 실행' }).click();
  const after = await page.evaluate(d => { const s = (window as any).__chunha.state(); return { x: s.cities.xuchang.troops, d: s.cities[d!].troops }; }, dest);
  expect(before.x - after.x).toBe(2000);
  expect(after.d - before.d).toBe(2000);
});

test('되돌리기는 새로고침 뒤에도 남고, 무엇으로 돌아갔는지 알려 준다', async ({ page }) => {
  await startCao(page);
  await page.getByRole('button', { name: '턴 종료' }).click();
  await closeReport(page);
  await page.getByRole('button', { name: '턴 종료' }).click();
  await closeReport(page);
  await page.reload();
  await page.getByRole('button', { name: /이어하기/ }).click();
  await closeReport(page);
  await expect(page.getByRole('button', { name: '되돌리기 2' })).toBeVisible();
  await page.getByRole('button', { name: '되돌리기 2' }).click();
  await expect(page.getByRole('status')).toHaveText('200년 2월 턴 종료 전으로 되돌렸다.');
  await expect(page.locator('.date')).toHaveText('200년 2월');
});

test('출진을 되돌리면 "출진 전"이라고 알려 준다', async ({ page }) => {
  await startCao(page);
  await page.getByRole('button', { name: '출진' }).first().click();
  await page.locator('[data-act="atk-target"][data-to="runan"]').click();
  await page.getByRole('button', { name: '출진', exact: true }).last().click();
  await page.getByRole('button', { name: '모의전으로 끝내기' }).click();
  const caps = page.getByRole('button', { name: '해방' });
  while (await caps.count()) await caps.first().click();
  await page.getByRole('button', { name: '돌아가기' }).click();
  await page.getByRole('button', { name: /되돌리기 1/ }).click();
  await expect(page.getByRole('status')).toHaveText('여남 출진 전으로 되돌렸다.');
});

test('팝업: 초점이 안에 머물고 Esc로 닫히면 연 버튼으로 돌아간다', async ({ page }) => {
  await startCao(page);
  const opener = page.locator('.actions [data-act="cmd"][data-tab="domestic"]');
  await opener.focus();
  await page.keyboard.press('Enter');
  const dlg = page.getByRole('dialog', { name: /명령/ });
  await expect(dlg).toBeVisible();
  for (let i = 0; i < 25; i++) {
    await page.keyboard.press('Tab');
    expect(await page.evaluate(() => !!document.activeElement?.closest('.modal'))).toBe(true);
  }
  await page.keyboard.press('Shift+Tab');
  expect(await page.evaluate(() => !!document.activeElement?.closest('.modal'))).toBe(true);
  await page.keyboard.press('Escape');
  await expect(dlg).toBeHidden();
  await expect(opener).toBeFocused();
});

test('전투 팝업은 Esc로 닫히지 않는다', async ({ page }) => {
  await startCao(page);
  await page.getByRole('button', { name: '출진' }).first().click();
  await page.locator('[data-act="atk-target"][data-to="runan"]').click();
  await page.getByRole('button', { name: '출진', exact: true }).last().click();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog', { name: /여남 전투/ })).toBeVisible();
});
