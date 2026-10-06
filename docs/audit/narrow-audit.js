// narrow-audit.js — 390px 窄屏全量审计（Playwright）
// 用法：node narrow-audit.js
const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');

const VIEWPORT = { width: 390, height: 844 };
const BASE = 'http://localhost:5783';
const PAGES = [
  '/inodes',
  '/metadata/units',
  '/metadata/option-sets',
  '/metadata/composite-types',
  '/metadata/entity-types',
  '/metadata/custom-tables',
  '/metadata/attributes',
  '/entities',
];

(async () => {
  const CHROME = require('os').homedir() +
    '/AppData/Local/ms-playwright/chromium-1148/chrome-win/chrome.exe';
  const browser = await chromium.launch({
    headless: true,
    executablePath: CHROME,
  });
  const context = await browser.newContext({ viewport: VIEWPORT });

  // 把审计脚本注入每个页面（addInitScript 在每次导航后执行）
  const auditSrc = fs.readFileSync(path.join(__dirname, 'audit.js'), 'utf8');
  await context.addInitScript({ content: auditSrc });

  const page = await context.newPage();

  const results = [];
  for (const route of PAGES) {
    try {
      await page.goto(BASE + route, { waitUntil: 'networkidle', timeout: 20000 });
      await page.waitForTimeout(2000);
      const res = await page.evaluate(() => {
        const s = auditPage();
        const detail = window.__audit_last.detail;
        const topOverflow = (detail.overflow.hits || []).slice(0, 10)
          .map(h => ({ tag: h.tag, cls: h.cls, text: h.text, w: h.width, right: h.right }));
        const topFixedW = (detail.fixedW.hits || []).slice(0, 5)
          .map(h => ({ tag: h.tag, cls: h.cls, text: h.text, minWidth: h.minWidth, style: h.styleAttr }));
        const topTouch = (detail.touch.hits || []).filter(h => !h.isComponentDefault).slice(0, 10)
          .map(h => ({ tag: h.tag, cls: h.cls, text: h.text, tooSmall: h.tooSmall }));
        return { summary: s, topOverflow, topFixedW, topTouch };
      });
      results.push({ route, ...res.summary, topOverflow: res.topOverflow, topFixedW: res.topFixedW, topTouch: res.topTouch });
      console.log(`✅ ${route}  overflow=${res.summary.overflow} fixedW=${res.summary.fixedWidth} narrowGrid=${res.summary.narrowGridItems} tableOverflow=${res.summary.tableOverflow} toolbarOverflow=${res.summary.toolbarOverflow} smallTouch=${res.summary.smallTouchTargets}`);
      if (res.topOverflow.length) console.log(`    溢出 top: ${JSON.stringify(res.topOverflow.slice(0, 3))}`);
      if (res.topTouch.length) console.log(`    触控 top: ${JSON.stringify(res.topTouch.slice(0, 3))}`);
    } catch (e) {
      results.push({ route, error: String(e.message || e) });
      console.log(`❌ ${route}  ${e.message || e}`);
    }
  }

  const summary = {
    viewport: VIEWPORT,
    totalPages: results.length,
    pages: results,
    totals: {
      overflow: results.reduce((s, p) => s + (p.overflow || 0), 0),
      fixedWidth: results.reduce((s, p) => s + (p.fixedWidth || 0), 0),
      narrowGrid: results.reduce((s, p) => s + (p.narrowGridItems || 0), 0),
      tableOverflow: results.reduce((s, p) => s + (p.tableOverflow || 0), 0),
      toolbarOverflow: results.reduce((s, p) => s + (p.toolbarOverflow || 0), 0),
      smallTouch: results.reduce((s, p) => s + (p.smallTouchTargets || 0), 0),
    },
  };
  console.log('\n=== 汇总（390×844 窄屏）===');
  console.log(JSON.stringify(summary.totals, null, 2));

  fs.writeFileSync(path.join(__dirname, 'narrow-summary.json'), JSON.stringify(summary, null, 2));
  console.log(`\n详细结果写入 narrow-summary.json`);

  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
