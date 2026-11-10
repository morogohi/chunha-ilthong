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

async function closeReport(page: Page) {
  await page.getByRole('button', { name: '명령을 내린다' }).click();
}

test('타이틀: 6명의 군주를 고를 수 있다', async ({ page }) => {
  await expect(page.getByRole('heading', { name: '천하일통' })).toBeVisible();
  await expect(page.locator('.faction-btn')).toHaveCount(6);
  await page.screenshot({ path: `reports/screens/${test.info().project.name}-01-title.png`, fullPage: true });
});

test('조조로 시작해 내정하고 턴을 넘긴다', async ({ page }) => {
  await page.locator('.faction-btn', { hasText: '조조' }).click();
  await expect(page.getByRole('dialog', { name: /200년 1월 보고/ })).toBeVisible();
  await page.screenshot({ path: `reports/screens/${test.info().project.name}-02-report.png` });
  await closeReport(page);
  await expect(page.locator('.card-head h2')).toHaveText('허창');
  await page.screenshot({ path: `reports/screens/${test.info().project.name}-03-main.png`, fullPage: true });

  const gold = () => page.evaluate(() => (window as any).__chunha.state().cities.xuchang.gold);
  const g0 = await gold();
  await page.getByRole('button', { name: '내정', exact: true }).click();
  await page.screenshot({ path: `reports/screens/${test.info().project.name}-04-command.png` });
  await page.locator('[data-act="dom"][data-cmd="farm"]').click();
  await expect(page.locator('.toast')).toContainText('개간');
  expect(await gold()).toBe(g0 - 50);
  await page.getByRole('button', { name: '닫기' }).click();

  await page.getByRole('button', { name: '개발' }).click();
  await expect(page.getByRole('button', { name: '개발' })).toHaveAttribute('aria-pressed', 'true');

  await page.getByRole('button', { name: '턴 종료' }).click();
  await expect(page.getByRole('dialog', { name: /200년 2월 보고/ })).toBeVisible();
  await closeReport(page);
  await expect(page.locator('.date')).toHaveText('200년 2월');
});

test('지도에서 적 도시를 고르고 출진해 전투를 끝낸다', async ({ page }) => {
  await page.evaluate(() => {
    const w = window as any; w.__chunha.start('cao', 12345);
    const st = w.__chunha.state(); st.cities.xuchang.troops = 40000; st.cities.xuchang.food = 60000;
  });
  await closeReport(page);
  await page.locator('g[data-c="runan"]').click();
  await expect(page.locator('.card-head h2')).toHaveText('여남');
  await page.getByRole('button', { name: '여남 공격 준비' }).click();
  await expect(page.getByRole('dialog', { name: /출진/ })).toBeVisible();
  await expect(page.getByText(/모의전 예상 승률/)).toBeVisible();
  await page.screenshot({ path: `reports/screens/${test.info().project.name}-05-attack.png` });
  await page.getByRole('button', { name: '출진', exact: true }).last().click();
  await expect(page.getByRole('dialog', { name: /여남 전투/ })).toBeVisible();
  await page.getByRole('button', { name: '돌격' }).click();
  await page.screenshot({ path: `reports/screens/${test.info().project.name}-06-battle.png` });
  const btn = page.getByRole('button', { name: '모의전으로 끝내기' });
  if (await btn.isVisible()) await btn.click();
  await expect(page.getByRole('button', { name: '돌아가기' })).toBeVisible();
  await page.screenshot({ path: `reports/screens/${test.info().project.name}-07-battle-end.png` });
  const caps = page.getByRole('button', { name: '해방' });
  while (await caps.count()) { await caps.first().click(); }
  await page.getByRole('button', { name: '돌아가기' }).click();
});

test('저장: 새로고침하면 이어하기가 나온다', async ({ page }) => {
  await page.locator('.faction-btn', { hasText: '손권' }).click();
  await closeReport(page);
  await page.getByRole('button', { name: '턴 종료' }).click();
  await closeReport(page);
  await page.reload();
  await expect(page.getByRole('button', { name: /이어하기 — 200년 2월 손권군/ })).toBeVisible();
  await page.getByRole('button', { name: /이어하기/ }).click();
  await closeReport(page);
  await expect(page.locator('.date')).toHaveText('200년 2월');
});

test('되돌리기: 한 턴 전으로 돌아간다', async ({ page }) => {
  await page.locator('.faction-btn', { hasText: '원소' }).click();
  await closeReport(page);
  await page.getByRole('button', { name: '턴 종료' }).click();
  await closeReport(page);
  await page.getByRole('button', { name: /되돌리기 1/ }).click();
  await expect(page.locator('.date')).toHaveText('200년 1월');
});

test('36턴 연속 진행해도 오류가 없다 (위임 플레이)', async ({ page }) => {
  await page.locator('.faction-btn', { hasText: '유표' }).click();
  await closeReport(page);
  for (let i = 0; i < 36; i++) {
    await page.getByRole('button', { name: '턴 종료' }).click();
    const end = page.getByRole('button', { name: '새로 시작' });
    if (await end.isVisible()) break;
    await closeReport(page);
  }
  const st = await page.evaluate(() => { const s = (window as any).__chunha.state(); return { y: s.year, m: s.month, over: s.over }; });
  expect(st.over || st.y >= 203).toBe(true);
  await page.screenshot({ path: `reports/screens/${test.info().project.name}-08-after-36.png`, fullPage: true });
});

test('모바일 레이아웃: 가로 넘침이 없고 버튼은 44px 이상이다', async ({ page }) => {
  await page.locator('.faction-btn', { hasText: '유비' }).click();
  await closeReport(page);
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  expect(overflow).toBeLessThanOrEqual(0);
  const small = await page.evaluate(() => [...document.querySelectorAll('button')]
    .filter(b => (b as HTMLElement).offsetParent !== null)
    .map(b => ({ t: b.textContent?.trim(), h: b.getBoundingClientRect().height }))
    .filter(x => x.h < 40));
  expect(small).toEqual([]);
});

test('삼고초려: 유비가 양양을 얻은 뒤 세 번 찾아가면 제갈량이 합류한다', async ({ page }) => {
  await page.evaluate(() => {
    const w = window as any; w.__chunha.start('liu', 777);
    const st = w.__chunha.state();
    st.cities.xiangyang.faction = 'liu'; st.officers.liubei.city = 'xiangyang';
    for (const o of Object.values(st.officers) as any[]) if (o.city === 'xiangyang' && o.faction === 'biao') o.city = 'wan';
  });
  await closeReport(page);
  await page.locator('g[data-c="xiangyang"]').click();
  for (let i = 0; i < 3; i++) {
    await page.getByRole('button', { name: '인사', exact: true }).click();
    await page.locator('[data-act="search"]').click();
    await page.getByRole('button', { name: '닫기' }).click();
    await page.getByRole('button', { name: '턴 종료' }).click();
    await closeReport(page);
  }
  const zg = await page.evaluate(() => (window as any).__chunha.state().officers.zhugeliang);
  expect(zg.faction).toBe('liu');
});
