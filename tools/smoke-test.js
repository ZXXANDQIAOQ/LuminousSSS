// Luminous 冒烟测试：登录界面 → 进入游戏 → 移动 → 碰撞 → 截图
// 依赖：npm install playwright-core（调用系统 Edge，无需下载浏览器）
// 用法：先起本地服务 python -m http.server 8765，再 node tools/smoke-test.js
const { chromium } = require('playwright-core');
const os = require('os');
const path = require('path');

const OUT = path.join(os.tmpdir(), 'luminous_test');
const BASE = process.env.GAME_URL || 'http://127.0.0.1:8765/index.html';

(async () => {
  const fs = require('fs');
  fs.mkdirSync(OUT, { recursive: true });

  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });

  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push('PAGEERROR: ' + e.message));

  await page.goto(BASE, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1200);
  await page.screenshot({ path: path.join(OUT, '01_login.png') });

  await page.fill('#nickname', '测试旅人');
  await page.click('#btnStart');
  await page.waitForTimeout(1000);
  await page.screenshot({ path: path.join(OUT, '02_world_spawn.png') });

  await page.keyboard.down('ArrowRight');
  await page.waitForTimeout(1200);
  await page.keyboard.up('ArrowRight');
  await page.waitForTimeout(300);
  await page.screenshot({ path: path.join(OUT, '03_moved_right.png') });

  await page.keyboard.down('ArrowUp');
  await page.waitForTimeout(2500);
  await page.keyboard.up('ArrowUp');
  await page.waitForTimeout(300);
  await page.screenshot({ path: path.join(OUT, '04_moved_up.png') });

  const state = await page.evaluate(() => ({
    title: document.title,
    loginHidden: document.getElementById('login').classList.contains('hidden'),
  }));

  console.log('STATE:', JSON.stringify(state));
  console.log('CONSOLE_ERRORS:', errors.length ? JSON.stringify(errors, null, 2) : 'none');
  console.log('SCREENSHOTS:', OUT);

  await browser.close();
  if (errors.length) process.exit(2);
})().catch((e) => { console.error('TEST FAILED:', e.message); process.exit(1); });
