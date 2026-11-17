// Chat gia đình – nhân viên realtime (SignalR) bằng hai trình duyệt thật: tin mới, "Đã xem", "đang nhập…", nối lại sau khi mất kết nối.
// Ghi tin nhắn thử vào cuộc trò chuyện số 1 (cư dân NH-0001, tài khoản family) của DB đang chạy: chạy trên DB test/demo, xem README.md.
import path from 'node:path';
import { createRequire } from 'node:module';
import { chromium } from 'playwright-core';

const FE = process.env.FE_BASE ?? 'http://localhost:5173';
const API = process.env.API_BASE ?? 'http://localhost:5080';
const OUT = path.resolve(import.meta.dirname, 'shots');
const CONVERSATION = Number(process.env.CHAT_CONVERSATION ?? 1);
const RUN = Date.now().toString().slice(-6);

const login = async (u) => (await (await fetch(`${API}/api/auth/login`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ email: `${u}@nursinghome.local`, password: 'Demo@123456' }) })).json());
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const open = async (user, url) => {
  const s = await login(user);
  const ctx = await browser.newContext({ viewport: { width: 1300, height: 800 } });
  await ctx.addInitScript(([a, r]) => { localStorage.setItem('nursinghome.accessToken', a); localStorage.setItem('nursinghome.refreshToken', r); }, [s.accessToken, s.refreshToken]);
  const page = await ctx.newPage();
  page.errors = [];
  page.on('pageerror', (e) => page.errors.push(String(e)));
  await page.goto(FE + url, { waitUntil: 'networkidle' });
  return page;
};

let fails = 0;
const t = async (name, fn) => {
  try { await fn(); console.log('PASS ', name); } catch (e) { fails++; console.log('FAIL ', name, '\n      ', String(e).split('\n')[0].slice(0, 220)); }
};
const box = (page) => page.locator('textarea[placeholder*="Nhập tin nhắn"]');
const send = async (page, text) => { await box(page).fill(text); await page.keyboard.press('Enter'); };
const typing = (page) => page.getByText(/đang nhập…/).first();

const family = await open('family', `/family/messages?c=${CONVERSATION}`);
const nurse = await open('nurse', `/staff/messages?c=${CONVERSATION}`);
const fromFamily = `QA-RT gia đình ${RUN}`;
const fromNurse = `QA-RT điều dưỡng ${RUN}`;

await t('gia đình gửi → điều dưỡng thấy ngay (không tải lại)', async () => {
  await send(family, fromFamily);
  await nurse.getByText(fromFamily).first().waitFor({ timeout: 4000 });
});
await t('điều dưỡng trả lời → gia đình thấy ngay', async () => {
  await send(nurse, fromNurse);
  await family.getByText(fromNurse).first().waitFor({ timeout: 4000 });
});
await t('"Đã xem" hiện ngay ở người gửi khi bên kia đang mở khung chat', async () => {
  await nurse.getByText(fromNurse, { exact: true }).locator('xpath=following-sibling::div[contains(., "Đã xem")]').first().waitFor({ timeout: 4000 });
});
await t('điều dưỡng gõ chữ → gia đình thấy "đang nhập…", rồi tự tắt khi ngừng gõ', async () => {
  await nurse.waitForTimeout(2200); // sau lần gửi tin ở trên (bản thân việc gõ tin đã báo "đang nhập"), chờ qua khoảng giãn cách
  await box(nurse).fill('');
  await box(nurse).pressSequentially('abc', { delay: 80 });
  await typing(family).waitFor({ timeout: 3000 });
  await typing(family).waitFor({ state: 'hidden', timeout: 8000 });
});
await t('gia đình gõ chữ → điều dưỡng thấy "đang nhập…" ở khung chat và cả ở danh sách', async () => {
  await box(family).pressSequentially('xyz', { delay: 80 });
  await typing(nurse).waitFor({ timeout: 3000 });
  if ((await nurse.getByText(/đang nhập…/).count()) < 2) throw new Error('danh sách bên trái không hiện "đang nhập…"');
});
await t('tin nhắn tới thì "đang nhập…" biến mất ngay', async () => {
  await send(family, `QA-RT gia đình hai ${RUN}`);
  await nurse.getByText(`QA-RT gia đình hai ${RUN}`).first().waitFor({ timeout: 4000 });
  await typing(nurse).waitFor({ state: 'hidden', timeout: 1500 });
});
await t('tài khoản ngoài cuộc trò chuyện (family2) gọi Typing: bị bỏ qua, không ai thấy "đang nhập…"', async () => {
  const { HubConnectionBuilder } = createRequire(path.resolve(import.meta.dirname, '../../FE/package.json'))('@microsoft/signalr');
  const session = await login('family2');
  const connection = new HubConnectionBuilder().withUrl(`${API}/hubs/notifications`, { accessTokenFactory: () => session.accessToken }).build();
  await connection.start();
  await connection.invoke('Typing', CONVERSATION);
  await connection.stop();
  await nurse.waitForTimeout(1500);
  if (await typing(nurse).isVisible()) throw new Error('người ngoài cuộc trò chuyện vẫn gây ra "đang nhập…"');
});
await t('mất kết nối rồi nối lại: tin gửi trong lúc mất kết nối tự hiện lại, không cần tải trang', async () => {
  const missed = `QA-RT lúc mất mạng ${RUN}`;
  await nurse.context().setOffline(true);
  await nurse.waitForTimeout(500);
  await send(family, missed);
  await nurse.waitForTimeout(1500);
  if (await nurse.getByText(missed).count()) throw new Error('điều dưỡng vẫn nhận được khi đang offline (không mô phỏng được mất kết nối)');
  await nurse.context().setOffline(false);
  await nurse.getByText(missed).first().waitFor({ timeout: 25000 });
});

for (const page of [family, nurse]) if (page.errors.length) { fails++; console.log('FAIL  lỗi trang:', page.errors.slice(0, 3)); }
await browser.close();
console.log(fails === 0 ? '\nTẤT CẢ PASS' : `\n${fails} FAIL`);
process.exit(fails ? 1 : 0);
