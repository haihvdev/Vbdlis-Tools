#!/usr/bin/env node
/**
 * OpenClaw - VBDLIS CungCapThongTinGiayChungNhan search (headless Playwright).
 *
 * Usage:
 *   node gcn_search.js --soGiayTo 027083000849
 *   node gcn_search.js --soPhatHanh "DL 071888"
 *   node gcn_search.js --soGiayTo "027083000849, 027083000850" --json
 *   node gcn_search.js --soGiayTo "027083000849" --soPhatHanh "DL 071888"
 *
 * Options:
 *   --profileDir <dir>    Persistent profile dir (default: ./mplis-profile)
 *   --headless <true|false> (default: true)
 *   --json                Print JSON instead of pretty text
 *   --maxQueries <n>       Safety limit for batch (default: 50)
 *   --delayMs <n>          Delay between queries (default: 300)
 *
 * Credentials (only needed if session expired and redirected to login):
 *   MPLIS_USER / MPLIS_PASS from .env or environment.
 */

const fs = require('fs');
const path = require('path');

function getArg(name) {
  const idx = process.argv.indexOf(name);
  if (idx === -1) return null;
  const v = process.argv[idx + 1];
  if (!v || v.startsWith('--')) return '';
  return v;
}
function hasFlag(name) {
  return process.argv.includes(name);
}

function parseList(raw) {
  if (!raw) return [];
  return String(raw)
    .split(/[\n,;]+/)
    .map((s) => s.trim())
    .filter(Boolean)
    .filter((v, i, arr) => arr.indexOf(v) === i);
}

const soGiayToList = parseList(getArg('--soGiayTo'));
const soPhatHanhList = parseList(getArg('--soPhatHanh'));
const profileDir = getArg('--profileDir') || './mplis-profile';
const headlessRaw = getArg('--headless');
const headless = headlessRaw == null ? true : !['false', '0', 'no'].includes(String(headlessRaw).toLowerCase());
const asJson = hasFlag('--json');
const maxQueries = Math.max(1, parseInt(getArg('--maxQueries') || '50', 10) || 50);
const delayMs = Math.max(0, parseInt(getArg('--delayMs') || '300', 10) || 300);

if (!soGiayToList.length && !soPhatHanhList.length) {
  console.error('ERR: Provide --soGiayTo and/or --soPhatHanh');
  process.exit(2);
}

const queries = [];
if (soGiayToList.length && soPhatHanhList.length) {
  for (const soGiayTo of soGiayToList) {
    for (const soPhatHanh of soPhatHanhList) {
      queries.push({ soGiayTo, soPhatHanh });
    }
  }
} else if (soGiayToList.length) {
  for (const soGiayTo of soGiayToList) {
    queries.push({ soGiayTo, soPhatHanh: null });
  }
} else {
  for (const soPhatHanh of soPhatHanhList) {
    queries.push({ soGiayTo: null, soPhatHanh });
  }
}

if (queries.length > maxQueries) {
  console.error(`ERR: Too many queries (${queries.length}). Raise --maxQueries if intentional.`);
  process.exit(2);
}

// Load .env if present (optional)
try {
  const dotenvPath = path.resolve(process.cwd(), '.env');
  if (fs.existsSync(dotenvPath)) {
    // lazy require so skill can run even if dotenv isn't installed
    require('dotenv').config({ path: dotenvPath, override: false, quiet: true });
  }
} catch {}

const { chromium } = require('playwright');

(async () => {
  const url = 'https://bgi.mplis.gov.vn/dc/CungCapThongTinGiayChungNhan/Index';

  const context = await chromium.launchPersistentContext(profileDir, {
    headless,
  });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  page.setDefaultNavigationTimeout(60000);

  await page.goto(url, { waitUntil: 'domcontentloaded' });

  // If redirected to login, attempt login using env.
  if (page.url().includes('/account/login')) {
    const user = process.env.MPLIS_USER;
    const pass = process.env.MPLIS_PASS;
    if (!user || !pass) {
      throw new Error('Redirected to login but MPLIS_USER/MPLIS_PASS not set.');
    }
    await page.fill('input[placeholder="Tên tài khoản"]', user);
    await page.fill('input[placeholder="Mật khẩu"]', pass);
    await Promise.all([
      page.waitForNavigation({ waitUntil: 'domcontentloaded' }),
      page.getByRole('button', { name: 'ĐĂNG NHẬP' }).click(),
    ]);
    // Go again to target page after login.
    await page.goto(url, { waitUntil: 'domcontentloaded' });
  }

  // Wait for core UI.
  await page.waitForSelector('#cung_cap_thong_tin_wrapper', { timeout: 60000 });
  await page.waitForSelector('#btnTraCuuGiayChungNhan', { timeout: 60000 });
  await page.waitForSelector('#tblTraCuuGiayChungNhan', { timeout: 60000 });

  const results = [];

  for (let qi = 0; qi < queries.length; qi++) {
    const query = queries[qi];
    try {
      // Fill fields (clear when missing).
      if (query.soGiayTo) {
        await page.locator('input[name="soGiayTo"]').fill(String(query.soGiayTo));
      } else {
        await page.locator('input[name="soGiayTo"]').fill('');
      }
      if (query.soPhatHanh) {
        await page.locator('input[name="soPhatHanh"]').fill(String(query.soPhatHanh));
      } else {
        await page.locator('input[name="soPhatHanh"]').fill('');
      }

      const beforeBody = await page.locator('#tblTraCuuGiayChungNhan tbody').innerText().catch(() => '');
      const beforeInfo = await page.locator('#tblTraCuuGiayChungNhan_info').innerText().catch(() => '');

      await page.locator('#btnTraCuuGiayChungNhan').click();
      await page.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});

      // Wait for table to update (best-effort).
      await page
        .waitForFunction(
          (prevBody, prevInfo) => {
            const body = document.querySelector('#tblTraCuuGiayChungNhan tbody');
            const info = document.querySelector('#tblTraCuuGiayChungNhan_info');
            const nowBody = (body?.innerText || '').trim();
            const nowInfo = (info?.innerText || '').trim();
            return nowBody !== (prevBody || '').trim() || nowInfo !== (prevInfo || '').trim();
          },
          beforeBody,
          beforeInfo,
          { timeout: 60000 }
        )
        .catch(() => {});

      await page.waitForTimeout(800);

      const info = (await page.locator('#tblTraCuuGiayChungNhan_info').innerText().catch(() => ''))
        .replace(/\s+/g, ' ')
        .trim();

      const rows = page.locator('#tblTraCuuGiayChungNhan tbody tr');
      const rowCount = await rows.count();
      const outRows = [];

      for (let i = 0; i < rowCount; i++) {
        const row = rows.nth(i);
        if (await row.locator('td.dataTables_empty').count()) continue;

        const cells = row.locator('td');
        const n = await cells.count();
        const arr = [];
        for (let j = 0; j < n; j++) {
          arr.push((await cells.nth(j).innerText()).replace(/\s+/g, ' ').trim());
        }
        outRows.push(arr);
      }

      results.push({
        query,
        url: page.url(),
        info,
        count: outRows.length,
        rows: outRows,
      });
    } catch (e) {
      results.push({
        query,
        error: e?.message || String(e),
      });
    }

    if (delayMs > 0 && qi < queries.length - 1) {
      await page.waitForTimeout(delayMs);
    }
  }

  if (asJson) {
    console.log(JSON.stringify({ queries, results }, null, 2));
  } else {
    console.log(`Tong so truy van: ${results.length}`);
    for (let i = 0; i < results.length; i++) {
      const r = results[i];
      console.log(`\n[${i + 1}] soGiayTo=${r.query?.soGiayTo || ''} | soPhatHanh=${r.query?.soPhatHanh || ''}`);
      if (r.error) {
        console.log(`- Loi: ${r.error}`);
        continue;
      }
      console.log(`- URL: ${r.url}`);
      console.log(`- Trang thai: ${r.info || '(no info)'}`);
      console.log(`- So dong: ${r.count}`);
      for (let j = 0; j < r.rows.length; j++) {
        const [idx, gcn, owner, land] = r.rows[j];
        console.log(`  - Dong ${j + 1}:`);
        if (gcn) console.log(`    Thong tin GCN: ${gcn}`);
        if (owner) console.log(`    Chu so huu: ${owner}`);
        if (land) console.log(`    Thua dat/Can ho: ${land}`);
      }
    }
  }

  await context.close();
})().catch((e) => {
  console.error('ERR:', e?.message || e);
  process.exit(1);
});
