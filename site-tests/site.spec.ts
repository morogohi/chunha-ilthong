import { test, expect } from '@playwright/test';
const errs: string[] = [];
test.beforeEach(async ({ page }) => {
  errs.length = 0;
  page.on('pageerror', e => errs.push(e.message));
  page.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await page.route(/fonts\.(googleapis|gstatic)\.com/, r => r.fulfill({ status: 200, contentType: 'text/css', body: '' }));
});
test.afterEach(() => expect(errs).toEqual([]));

test('랜딩: 제목·CTA·섹션이 보이고 가로 넘침이 없다', async ({ page }, info) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1, name: '천하일통' })).toBeVisible();
  await expect(page.getByRole('link', { name: '브라우저로 바로 해보기' })).toHaveAttribute('href', 'play/');
  await page.waitForTimeout(2600); // 히어로 등장 연출
  await page.screenshot({ path: `shots/${info.project.name}-hero.png` });
  for (const id of ['game', 'heroes', 'features', 'faq']) await expect(page.locator(`#${id}`)).toBeAttached();
  expect(await page.locator('.hero-card').count()).toBe(10);
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - innerWidth);
  expect(overflow).toBeLessThanOrEqual(0);
  // 스크롤하며 등장 연출을 모두 켠 뒤 전체 캡처
  for (let y = 0; y < 12000; y += 600) { await page.mouse.wheel(0, 600); await page.waitForTimeout(60); }
  await page.waitForTimeout(900);
  await page.screenshot({ path: `shots/${info.project.name}-full.png`, fullPage: true });
});

test('모든 이미지에 대체 텍스트가 있고 실제로 불러와진다', async ({ page }) => {
  await page.goto('/');
  const imgs = page.locator('img');
  for (let i = 0; i < await imgs.count(); i++) {
    const img = imgs.nth(i);
    await img.scrollIntoViewIfNeeded();
    expect((await img.getAttribute('alt'))?.length).toBeGreaterThan(5);
    await expect.poll(() => img.evaluate((el: HTMLImageElement) => el.complete && el.naturalWidth > 0)).toBe(true);
  }
});

test('내부 링크가 모두 열린다 (체험판·정책·지원·OG 이미지)', async ({ page, request }) => {
  for (const p of ['/play/', '/privacy.html', '/support.html', '/assets/og.png', '/assets/icon.png', '/assets/icon-1024.png']) {
    const r = await request.get(p); expect(r.status(), p).toBe(200);
  }
  await page.goto('/');
  const hrefs = await page.locator('a[href]').evaluateAll(as => as.map(a => (a as HTMLAnchorElement).getAttribute('href')!));
  for (const h of hrefs.filter(h => !h.startsWith('http') && !h.startsWith('#'))) {
    const r = await request.get(new URL(h, 'http://127.0.0.1:4180/').pathname); expect(r.status(), h).toBe(200);
  }
});

test('체험판: 사이트에서 들어가 바로 플레이할 수 있다', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('link', { name: '브라우저로 바로 해보기' }).click();
  await expect(page).toHaveURL(/\/play\/$/);
  await page.locator('.faction-btn', { hasText: '손권' }).click();
  await page.getByRole('button', { name: '명령을 내린다' }).click();
  await page.getByRole('button', { name: '턴 종료' }).click();
  await expect(page.getByRole('dialog', { name: /200년 2월 보고/ })).toBeVisible();
});

test('정책·지원 페이지: 스토어 심사에 필요한 내용이 있다', async ({ page }) => {
  await page.goto('/privacy.html');
  await expect(page.getByRole('heading', { name: '개인정보처리방침' })).toBeVisible();
  await expect(page.getByText('개인정보를 수집하지 않습니다').first()).toBeVisible();
  await expect(page.getByText('Google AdMob', { exact: false }).first()).toBeVisible();
  await expect(page.getByText(/시행일 \d{4}년/)).toBeVisible();
  await page.goto('/support.html');
  await expect(page.getByRole('link', { name: '새 이슈 쓰기' })).toHaveAttribute('href', /github\.com\/morogohi\/chunha-ilthong\/issues\/new/);
});

test('공유 미리보기 메타 태그', async ({ page }) => {
  await page.goto('/');
  for (const p of ['og:title', 'og:description', 'og:image']) expect(await page.locator(`meta[property="${p}"]`).getAttribute('content')).toBeTruthy();
  expect(await page.locator('meta[name="description"]').getAttribute('content')).toContain('천하일통');
});
