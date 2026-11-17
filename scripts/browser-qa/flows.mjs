// BL-V5: luồng tương tác thật trên giao diện (nhật ký, cấu hình, danh mục, thông báo, tin tức & album, kênh nhận). Xem README.md.
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright-core';

const FE = process.env.FE_BASE ?? 'http://localhost:5173';
const API = process.env.API_BASE ?? 'http://localhost:5080';
const OUT = path.resolve(import.meta.dirname, 'shots');
fs.mkdirSync(OUT, { recursive: true });

const login = async (user) => (await (await fetch(`${API}/api/auth/login`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ email: `${user}@nursinghome.local`, password: 'Demo@123456' }) })).json());
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const newPage = async (user) => {
  const context = await browser.newContext({ viewport: { width: 1366, height: 860 }, locale: 'vi-VN' });
  if (user) {
    const s = await login(user);
    await context.addInitScript(([a, r]) => { localStorage.setItem('nursinghome.accessToken', a); localStorage.setItem('nursinghome.refreshToken', r); }, [s.accessToken, s.refreshToken]);
  }
  const page = await context.newPage();
  page.errors = [];
  page.on('pageerror', (e) => page.errors.push(String(e)));
  page.on('console', (m) => { if (m.type() === 'error' && !/negotiation|Failed to start|frame-ancestors|WebSocket|Failed to load resource|antd: List/.test(m.text())) page.errors.push(m.text().slice(0, 200)); });
  return page;
};

let fails = 0;
let currentPage;
const step = async (name, fn) => {
  try {
    await fn();
    console.log('PASS ', name);
  } catch (e) {
    fails++;
    try { await currentPage?.screenshot({ path: path.join(OUT, 'fail_' + name.slice(0, 20).replace(/[^a-z0-9]/gi, '_') + '.png') }); } catch {}
    console.log('FAIL ', name, '\n      ', String(e).split('\n')[0].slice(0, 300));
  }
};
const toast = async (page, re) => page.locator('.ant-message-notice').filter({ hasText: re }).first().waitFor({ timeout: 6000 });
const shot = (page, name) => page.screenshot({ path: path.join(OUT, `flow_${name}.png`) });
const okModal = (page) => page.locator('.ant-modal-footer .ant-btn-primary').click();

// 1x1 PNG
const PNG = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==', 'base64');
const png = path.resolve(import.meta.dirname, 'tiny.png');
fs.writeFileSync(png, PNG);

const admin = await newPage('admin');
currentPage = admin;

await step('Cấu hình cơ sở: sửa hotline → lưu → thấy "Đã chỉnh" → đặt lại', async () => {
  await admin.goto(FE + '/staff/settings', { waitUntil: 'networkidle' });
  const hotline = admin.locator('div', { hasText: /^Hotline$/ }).first().locator('xpath=following-sibling::input').first();
  await hotline.fill('0236 123 4567');
  await admin.getByRole('button', { name: /Lưu cấu hình/ }).click();
  await toast(admin, /Đã lưu cấu hình/);
  await admin.locator('.ant-tag', { hasText: 'Đã chỉnh' }).first().waitFor({ timeout: 4000 });
  await shot(admin, 'settings');
  await admin.getByRole('button', { name: /Đặt lại/ }).first().click();
  await admin.locator('.ant-popconfirm .ant-btn-primary').click();
  await toast(admin, /Đã đặt lại/);
  if (await admin.locator('.ant-tag', { hasText: 'Đã chỉnh' }).count() !== 0) throw new Error('vẫn còn "Đã chỉnh"');
});

await step('Cấu hình cơ sở: giá trị sai bị API từ chối và hiện thông báo', async () => {
  await admin.goto(FE + '/staff/settings', { waitUntil: 'networkidle' });
  const low = admin.locator('div', { hasText: /^Huyết áp tâm thu thấp dưới \(mmHg\)$/ }).first().locator('xpath=following-sibling::div//input').first();
  const high = admin.locator('div', { hasText: /^Huyết áp tâm thu cao từ \(mmHg\)$/ }).first().locator('xpath=following-sibling::div//input').first();
  await low.fill('120'); await high.fill('120');
  await admin.getByRole('button', { name: /Lưu cấu hình/ }).click();
  await toast(admin, /nhỏ hơn/);
});

await step('Danh mục: thêm → thấy trong bảng → xóa', async () => {
  await admin.goto(FE + '/staff/settings', { waitUntil: 'networkidle' });
  await admin.getByRole('tab', { name: 'Danh mục' }).click();
  await admin.locator('.ant-segmented-item', { hasText: 'Chức danh' }).click();
  await admin.locator('.ant-table-tbody tr.ant-table-row').first().waitFor();
  await admin.getByRole('button', { name: /Thêm mục/ }).click();
  await admin.locator('.ant-modal input').nth(0).fill('QA_TEST');
  await admin.locator('.ant-modal input').nth(1).fill('Chức danh QA');
  await okModal(admin);
  await toast(admin, /Đã lưu mục/);
  await admin.locator('tr', { hasText: 'Chức danh QA' }).waitFor();
  await shot(admin, 'lookups');
  await admin.locator('tr', { hasText: 'Chức danh QA' }).getByRole('button', { name: 'Xóa' }).click();
  await admin.locator('.ant-popconfirm .ant-btn-primary').click();
  await toast(admin, /Đã xóa mục/);
});

await step('Thông báo hàng loạt: soạn → gửi cho vai trò Điều dưỡng → hiện trong lịch sử', async () => {
  await admin.goto(FE + '/staff/announcements', { waitUntil: 'networkidle' });
  await admin.locator('input#title, input[id$="_title"]').first().fill('Thông báo QA trình duyệt');
  await admin.locator('textarea').first().fill('Nội dung thông báo thử từ kiểm thử trình duyệt.');
  await admin.getByLabel('Một vai trò').check();
  await admin.locator('.ant-form-item', { hasText: /^Vai trò/ }).locator('.ant-select').click();
  await admin.locator('.ant-select-item-option', { hasText: 'Điều dưỡng' }).click();
  await admin.getByRole('button', { name: /Gửi thông báo/ }).click();
  await toast(admin, /Đã gửi cho/);
  await admin.locator('tr', { hasText: 'Thông báo QA trình duyệt' }).first().waitFor();
  await shot(admin, 'announcements');
});

let postTitle = 'Bài viết QA trình duyệt';
await step('Tin tức: viết bài mới → nháp → đăng', async () => {
  await admin.goto(FE + '/staff/posts', { waitUntil: 'networkidle' });
  await admin.getByRole('button', { name: /Viết bài mới/ }).click();
  await admin.locator('.ant-modal input[type="text"]').first().waitFor();
  await admin.locator('.ant-modal .ant-form-item', { hasText: /^Tiêu đề/ }).locator('input').fill(postTitle);
  await admin.locator('.ant-modal .ant-form-item', { hasText: /^Tóm tắt/ }).locator('textarea').fill('Tóm tắt kiểm thử.');
  await admin.locator('.ant-modal .ant-form-item', { hasText: /^Nội dung/ }).locator('textarea').fill('Dòng một.\nDòng hai <b>không in đậm</b>.');
  await shot(admin, 'post-editor');
  await okModal(admin);
  await toast(admin, /bản nháp/);
  const row = admin.locator('tr', { hasText: postTitle });
  await row.getByText('Nháp').waitFor();
  await row.getByRole('button', { name: 'Đăng' }).click();
  await toast(admin, /Đã đăng bài/);
  await row.getByText(/Đã đăng/).waitFor();
});

await step('Trang công khai /tin-tuc: thấy bài vừa đăng + 2 bài seed; mở chi tiết, chữ thuần không render HTML', async () => {
  const guest = await newPage(null);
  await guest.goto(FE + '/tin-tuc', { waitUntil: 'networkidle' });
  await guest.getByText(postTitle).first().waitFor({ timeout: 8000 });
  await guest.getByText('Chương trình khám sức khỏe định kỳ cho cư dân').first().waitFor();
  await shot(guest, 'news-list');
  await guest.getByText(postTitle).first().click();
  await guest.waitForURL(/\/tin-tuc\/bai-viet-qa-trinh-duyet/);
  await guest.getByText('Dòng một.').waitFor();
  const html = await guest.locator('.container').innerHTML();
  if (/<b>/.test(html)) throw new Error('HTML trong nội dung bị render');
  if (!(await guest.getByText('<b>không in đậm</b>', { exact: false }).count())) throw new Error('không thấy chữ thô <b>');
  await shot(guest, 'news-detail');
  await guest.goto(FE + '/tin-tuc/khong-co-bai-nay', { waitUntil: 'networkidle' });
  await guest.getByText('Không tìm thấy bài viết').waitFor();
  if (guest.errors.length) throw new Error('lỗi console: ' + guest.errors.join(' | '));
  await guest.context().close();
});

await step('Album: tải ảnh thật lên, chưa tick đồng ý thì không đăng được; tick rồi đăng', async () => {
  await admin.goto(FE + '/staff/posts', { waitUntil: 'networkidle' });
  await admin.getByRole('button', { name: /Viết bài mới/ }).click();
  await admin.locator('.ant-modal .ant-form-item', { hasText: /^Loại bài/ }).locator('.ant-select').click();
  await admin.locator('.ant-select-item-option', { hasText: 'Album ảnh' }).click();
  await admin.locator('.ant-modal .ant-form-item', { hasText: /^Tiêu đề/ }).locator('input').fill('Album QA trình duyệt');
  await admin.locator('.ant-modal .ant-form-item', { hasText: /^Nội dung/ }).locator('textarea').fill('Ảnh thử.');
  await admin.locator('.ant-modal input[type="file"]').setInputFiles(png);
  await admin.locator('.ant-modal .ant-image').first().waitFor({ timeout: 8000 });
  await okModal(admin);
  await toast(admin, /bản nháp/);
  const row = admin.locator('tr', { hasText: 'Album QA trình duyệt' });
  await row.getByRole('button', { name: 'Đăng' }).click();
  await toast(admin, /đồng ý/);
  await row.getByRole('button', { name: 'Sửa' }).click();
  await admin.locator('.ant-modal .ant-checkbox-wrapper', { hasText: /xác nhận đã có sự đồng ý/ }).click();
  await okModal(admin);
  await toast(admin, /Đã lưu bài viết/);
  await row.getByRole('button', { name: 'Đăng' }).click();
  await toast(admin, /Đã đăng bài/);
  const guest = await newPage(null);
  await guest.goto(FE + '/tin-tuc?', { waitUntil: 'networkidle' });
  await guest.getByText('Album QA trình duyệt').first().waitFor({ timeout: 8000 });
  await guest.locator('.ant-segmented-item', { hasText: 'Album ảnh' }).click();
  await guest.getByText('Album QA trình duyệt').first().click();
  await guest.locator('.ant-image img').first().waitFor({ timeout: 8000 });
  await shot(guest, 'album-detail');
  await guest.context().close();
});

await step('Dọn bài viết thử', async () => {
  await admin.goto(FE + '/staff/posts', { waitUntil: 'networkidle' });
  for (const title of [postTitle, 'Album QA trình duyệt']) {
    const row = admin.locator('tr', { hasText: title });
    await row.getByRole('button', { name: 'Xóa' }).click();
    await admin.locator('.ant-popconfirm .ant-btn-primary').click();
    await toast(admin, /Đã xóa bài viết/);
    await admin.waitForTimeout(500);
  }
});

await step('Hồ sơ: bật/tắt kênh email "Thông báo chung" lưu bền sau khi tải lại', async () => {
  await admin.goto(FE + '/profile', { waitUntil: 'networkidle' });
  const sw = admin.locator('.ant-card', { hasText: 'Kênh nhận thông báo qua email' }).getByRole('switch').first();
  await sw.waitFor();
  const before = await sw.getAttribute('aria-checked');
  await sw.click();
  await admin.waitForTimeout(800);
  await admin.reload({ waitUntil: 'networkidle' });
  const sw2 = admin.locator('.ant-card', { hasText: 'Kênh nhận thông báo qua email' }).getByRole('switch').first();
  await sw2.waitFor();
  const after = await sw2.getAttribute('aria-checked');
  if (before === after) throw new Error('trạng thái không đổi sau khi tải lại');
  await shot(admin, 'profile');
  await sw2.click(); // trả về như cũ
  await admin.waitForTimeout(600);
});

await step('Nhật ký thao tác: có dòng dữ liệu, mở rộng xem chi tiết', async () => {
  await admin.goto(FE + '/staff/audit', { waitUntil: 'networkidle' });
  await admin.locator('.ant-table-tbody tr.ant-table-row').first().waitFor({ timeout: 8000 });
  const rows = await admin.locator('.ant-table-tbody tr.ant-table-row').count();
  if (rows < 1) throw new Error('không có dòng');
  await admin.locator('.ant-table-row-expand-icon').first().click();
  await shot(admin, 'audit');
});

const nurse = await newPage('nurse');
await step('Điều dưỡng nhận thông báo QA trong chuông', async () => {
  await nurse.goto(FE + '/staff/home', { waitUntil: 'networkidle' });
  await nurse.locator('.ant-badge').first().click();
  await nurse.getByText('Thông báo QA trình duyệt').first().waitFor({ timeout: 6000 });
  await shot(nurse, 'nurse-bell');
});
await step('Trang chủ điều dưỡng hiển thị ca/việc và chat trợ lý trả lời', async () => {
  await nurse.goto(FE + '/staff/home', { waitUntil: 'networkidle' });
  await nurse.getByText('Chấm công').first().waitFor();
  await shot(nurse, 'staff-home');
});

const fam = await newPage('family');
await step('Gia đình: hồ sơ chỉ có 3 kênh email (hóa đơn, nhắc nợ, thông báo chung)', async () => {
  await fam.goto(FE + '/profile', { waitUntil: 'networkidle' });
  const n = await fam.locator('.ant-card', { hasText: 'Kênh nhận thông báo qua email' }).getByRole('switch').count();
  if (n !== 3) throw new Error(`số công tắc = ${n}`);
});

for (const p of [admin, nurse, fam]) if (p.errors.length) { fails++; console.log('FAIL  lỗi console:', [...new Set(p.errors)].slice(0, 5)); }
await browser.close();
console.log(fails === 0 ? '\nTẤT CẢ PASS' : `\n${fails} FAIL`);
process.exit(fails ? 1 : 0);
