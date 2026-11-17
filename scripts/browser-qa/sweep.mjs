// BL-V5 / S5-V1: duyệt mọi route của FE bằng Edge headless theo từng vai trò (xem README.md cùng thư mục).
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright-core';

const REPO = path.resolve(import.meta.dirname, '../../FE/src') + '/';
const OUT = path.resolve(import.meta.dirname, 'shots');
fs.mkdirSync(OUT, { recursive: true });
const FE = process.env.FE_BASE ?? 'http://localhost:5173';
const API = process.env.API_BASE ?? 'http://localhost:5080';

const routeSet = new Set();
for (const file of ['App.tsx', 'modules/operations.tsx', 'modules/care.tsx']) {
  const text = fs.readFileSync(REPO + file, 'utf8');
  for (const m of text.matchAll(/<Route\s+path="([^"]+)"/g)) routeSet.add(m[1]);
}
const routes = [...routeSet].filter((r) => r !== '*').map((r) => r.replace(/:id\b/g, '1').replace(/:slug\b/g, 'thong-bao-gio-tham-than'));

const roles = {
  Admin: 'admin', Receptionist: 'receptionist', Accountant: 'accountant', Doctor: 'doctor', Nurse: 'nurse', Caregiver: 'caregiver', Family: 'family',
};
const login = async (user) => {
  const res = await fetch(`${API}/api/auth/login`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ email: `${user}@nursinghome.local`, password: 'Demo@123456' }) });
  if (!res.ok) throw new Error(`login ${user}: ${res.status}`);
  return res.json();
};

const browser = await chromium.launch({ channel: 'msedge', headless: true });
const report = {};
const only = process.argv[2]; // vd. "Admin"
const shotRoutes = new Set(['/', '/tin-tuc', '/tin-tuc/thong-bao-gio-tham-than', '/staff/audit', '/staff/settings', '/staff/announcements', '/staff/posts', '/profile', '/staff/home', '/staff/hr', '/staff/room-ops']);

async function visitAll(label, session) {
  const context = await browser.newContext({ viewport: { width: 1366, height: 800 }, locale: 'vi-VN' });
  if (session) {
    await context.addInitScript(([a, r]) => {
      localStorage.setItem('nursinghome.accessToken', a);
      localStorage.setItem('nursinghome.refreshToken', r);
    }, [session.accessToken, session.refreshToken]);
  }
  const page = await context.newPage();
  const issues = [];
  let current = '';
  page.on('console', (msg) => {
    if (['error', 'warning'].includes(msg.type())) {
      const text = msg.text().replace(/\s+/g, ' ').slice(0, 300);
      if (!/Download the React DevTools|\[vite\]|Failed to start the connection|negotiation|WebSocket connection/.test(text)) issues.push({ route: current, kind: `console.${msg.type()}`, text });
    }
  });
  page.on('pageerror', (err) => issues.push({ route: current, kind: 'pageerror', text: String(err).slice(0, 300) }));
  page.on('response', (res) => {
    const status = res.status();
    if (status >= 400 && res.url().startsWith(API) && ![401].includes(status)) {
      issues.push({ route: current, kind: `http.${status}`, text: `${res.request().method()} ${res.url().replace(API, '')}` });
    }
  });
  for (const route of routes) {
    current = route;
    try {
      await page.goto(FE + route, { waitUntil: 'networkidle', timeout: 20000 });
      await page.waitForTimeout(400);
      const body = (await page.locator('body').innerText()).slice(0, 4000);
      const finalPath = new URL(page.url()).pathname;
      if (/Something went wrong|Unexpected Application Error|Cannot read properties|is not a function/.test(body)) issues.push({ route, kind: 'render', text: body.slice(0, 200) });
      if (finalPath !== route && !route.startsWith('/app')) {
        const note = finalPath === '/forbidden' ? '403-redirect' : finalPath === '/login' ? 'login-redirect' : `redirect->${finalPath}`;
        (report[label].redirects ??= {})[route] = note;
      }
      if (session && shotRoutes.has(route) && finalPath === route) await page.screenshot({ path: path.join(OUT, `${label}${route.replace(/\//g, '_')}.png`), fullPage: false });
    } catch (e) {
      issues.push({ route, kind: 'navigation', text: String(e).slice(0, 200) });
    }
  }
  await context.close();
  return issues;
}

for (const [label, user] of Object.entries({ Anonymous: null, ...roles })) {
  if (only && only !== label) continue;
  report[label] = {};
  const session = user ? await login(user) : null;
  const issues = await visitAll(label, session);
  // gộp trùng
  const seen = new Map();
  for (const i of issues) {
    const key = `${i.kind}|${i.text}`;
    if (!seen.has(key)) seen.set(key, { ...i, routes: [i.route] });
    else seen.get(key).routes.push(i.route);
  }
  report[label].issues = [...seen.values()].map(({ route, ...rest }) => rest);
  console.log(`\n=== ${label}: ${issues.length} issue(s), ${report[label].issues.length} distinct; redirects: ${Object.keys(report[label].redirects ?? {}).length}`);
  for (const i of report[label].issues) console.log(`  [${i.kind}] ${i.text}  <- ${[...new Set(i.routes)].slice(0, 4).join(', ')}${i.routes.length > 4 ? ` (+${i.routes.length - 4})` : ''}`);
}
fs.writeFileSync(path.resolve(import.meta.dirname, 'report.json'), JSON.stringify(report, null, 2));
await browser.close();
console.log('\nroutes visited:', routes.length);
