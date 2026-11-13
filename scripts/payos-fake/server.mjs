#!/usr/bin/env node
// PayOS giả lập cho máy local (không có tiền thật) — ý tưởng từ scripts/payos-fake của StreetBiz,
// rút gọn cho MỘT kênh nhận tiền của cơ sở. Nói đúng API merchant v2 mà NursingHome gọi:
//   POST /v2/payment-requests            tạo link (kiểm chữ ký bằng checksum key)
//   GET  /v2/payment-requests/{code}     trạng thái link (orderCode)
// và tự gửi webhook (ký đúng kiểu payOS) khi bấm "Khách đã trả" ở trang /dev.
//
//   node scripts/payos-fake/server.mjs
//   PAYOS_FAKE_PORT=5199  PAYOS_FAKE_WEBHOOK=http://localhost:5080/api/payments/payos/webhook
// API: đặt PayOs:ApiBaseUrl=http://localhost:5199 và 3 khóa demo bên dưới (appsettings.Development.example.json).
// Mở http://localhost:5199/dev để xem link, trả tiền, hủy, hoặc TẮT webhook để thử lớp 2 (đồng bộ).

import { createServer } from 'node:http';
import { createHmac, randomUUID } from 'node:crypto';

const PORT = Number(process.env.PAYOS_FAKE_PORT ?? 5199);
const BASE = `http://localhost:${PORT}`;
const WEBHOOK = process.env.PAYOS_FAKE_WEBHOOK ?? 'http://localhost:5080/api/payments/payos/webhook';
const CHANNEL = {
  clientId: 'nursinghome-demo-client-id',
  apiKey: 'nursinghome-demo-api-key',
  checksumKey: 'nursinghome-demo-checksum-key',
  bin: '970436', accountNumber: '0123456789', accountName: 'VIEN DUONG LAO AN KHANG',
};

/** @type {Map<number, any>} */
const links = new Map();
const log = [];
let webhooksEnabled = true;

const sign = (text) => createHmac('sha256', CHANNEL.checksumKey).update(text).digest('hex');
const sortedText = (obj) => Object.keys(obj).sort()
  .map((k) => `${k}=${obj[k] === null || obj[k] === undefined || obj[k] === 'null' || obj[k] === 'undefined' ? '' : obj[k]}`)
  .join('&');
const statusOf = (link) => (link.status === 'PENDING' && Date.now() / 1000 > link.expiredAt ? 'EXPIRED' : link.status);
const json = (res, status, body) => { res.writeHead(status, { 'content-type': 'application/json; charset=utf-8' }); res.end(JSON.stringify(body)); };
const ok = (res, data) => json(res, 200, { code: '00', desc: 'success', data, signature: data ? sign(sortedText(data)) : undefined });
const fail = (res, code, desc, status = 200) => json(res, status, { code, desc, data: null });
const readBody = (req) => new Promise((resolve) => { let s = ''; req.on('data', (c) => (s += c)); req.on('end', () => resolve(s)); });
const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

function describe(link) {
  const status = statusOf(link);
  return {
    id: link.paymentLinkId, orderCode: link.orderCode, amount: link.amount,
    amountPaid: status === 'PAID' ? link.amount : 0, amountRemaining: status === 'PAID' ? 0 : link.amount,
    status, createdAt: link.createdAt,
    transactions: link.reference ? [{ reference: link.reference, amount: link.amount, accountNumber: CHANNEL.accountNumber, description: link.description, transactionDateTime: link.paidAt }] : [],
  };
}

async function deliverWebhook(link) {
  const data = {
    orderCode: link.orderCode, amount: link.amount, description: link.description,
    accountNumber: CHANNEL.accountNumber, reference: link.reference, transactionDateTime: link.paidAt,
    currency: 'VND', paymentLinkId: link.paymentLinkId, code: '00', desc: 'success',
    counterAccountBankId: '', counterAccountBankName: '', counterAccountName: null, counterAccountNumber: null,
    virtualAccountName: '', virtualAccountNumber: '',
  };
  const body = { code: '00', desc: 'success', success: true, data, signature: sign(sortedText(data)) };
  if (!webhooksEnabled) { log.unshift(`orderCode ${link.orderCode}: webhook đang TẮT (thử lớp 2)`); return; }
  try {
    const r = await fetch(WEBHOOK, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
    log.unshift(`orderCode ${link.orderCode}: webhook -> HTTP ${r.status} ${await r.text()}`);
  } catch (e) {
    log.unshift(`orderCode ${link.orderCode}: webhook lỗi ${e.message}`);
  }
}

function devPage() {
  const rows = [...links.values()].reverse().map((l) => `<tr><td>${l.orderCode}</td><td>${l.amount.toLocaleString('vi-VN')} đ</td><td>${esc(l.description)}</td><td><b>${statusOf(l)}</b></td>
<td>${statusOf(l) === 'PENDING' ? `<form method="post" action="/dev/pay/${l.orderCode}" style="display:inline"><button>Khách đã trả</button></form>
<form method="post" action="/dev/cancel/${l.orderCode}" style="display:inline"><button>Hủy</button></form>` : ''}</td></tr>`).join('');
  return `<!doctype html><meta charset="utf-8"><title>PayOS giả lập</title>
<body style="font-family:sans-serif;max-width:960px;margin:24px auto">
<h2>PayOS giả lập — Nursing Home</h2>
<p>Webhook: <code>${esc(WEBHOOK)}</code> — <b>${webhooksEnabled ? 'đang bật' : 'đang TẮT (thử lớp đồng bộ)'}</b>
<form method="post" action="/dev/webhooks/${webhooksEnabled ? 'off' : 'on'}" style="display:inline"><button>${webhooksEnabled ? 'Tắt' : 'Bật'} webhook</button></form></p>
<table border="1" cellpadding="6" style="border-collapse:collapse;width:100%"><tr><th>orderCode</th><th>Số tiền</th><th>Nội dung</th><th>Trạng thái</th><th></th></tr>${rows || '<tr><td colspan=5>Chưa có link</td></tr>'}</table>
<h3>Nhật ký</h3><pre>${esc(log.slice(0, 30).join('\n'))}</pre><script>setTimeout(()=>location.reload(),5000)</script></body>`;
}

createServer(async (req, res) => {
  const url = new URL(req.url, BASE);
  const path = url.pathname.replace(/\/+$/, '') || '/';
  try {
    if (path.startsWith('/v2/') && (req.headers['x-client-id'] !== CHANNEL.clientId || req.headers['x-api-key'] !== CHANNEL.apiKey)) {
      return fail(res, '401', 'Client ID hoặc API Key không đúng', 401);
    }

    if (req.method === 'POST' && path === '/v2/payment-requests') {
      const b = JSON.parse(await readBody(req));
      const expected = sign(`amount=${b.amount}&cancelUrl=${b.cancelUrl}&description=${b.description}&orderCode=${b.orderCode}&returnUrl=${b.returnUrl}`);
      if (b.signature !== expected) return fail(res, '201', 'Chữ ký không hợp lệ');
      if (links.has(b.orderCode)) return fail(res, '231', 'Đơn thanh toán đã tồn tại');
      const link = {
        orderCode: b.orderCode, amount: b.amount, description: b.description, paymentLinkId: randomUUID().replaceAll('-', ''),
        status: 'PENDING', createdAt: new Date().toISOString(), expiredAt: b.expiredAt ?? Math.floor(Date.now() / 1000) + 1800,
      };
      links.set(b.orderCode, link);
      log.unshift(`Tạo link orderCode ${b.orderCode}: ${b.amount} đ`);
      // Chuỗi QR mô phỏng (định dạng EMVCo rút gọn) — đủ để FE vẽ QR.
      const qrCode = `00020101021238570010A000000727012700069704360113${CHANNEL.accountNumber}0208QRIBFTTA53037045405${b.amount}5802VN62${String(b.description.length + 4).padStart(2, '0')}08${String(b.description.length).padStart(2, '0')}${b.description}6304ABCD`;
      return ok(res, {
        bin: CHANNEL.bin, accountNumber: CHANNEL.accountNumber, accountName: CHANNEL.accountName, amount: b.amount,
        description: b.description, orderCode: b.orderCode, currency: 'VND', paymentLinkId: link.paymentLinkId,
        status: 'PENDING', checkoutUrl: `${BASE}/dev`, qrCode,
      });
    }

    const single = /^\/v2\/payment-requests\/(\d+)$/.exec(path);
    if (req.method === 'GET' && single) {
      const link = links.get(Number(single[1]));
      return link ? ok(res, describe(link)) : fail(res, '101', 'Không tìm thấy đơn thanh toán');
    }

    const devAction = /^\/dev\/(pay|cancel)\/(\d+)$/.exec(path);
    if (req.method === 'POST' && devAction) {
      const link = links.get(Number(devAction[2]));
      if (link && statusOf(link) === 'PENDING') {
        if (devAction[1] === 'pay') {
          link.status = 'PAID';
          link.reference = `FT${Date.now()}`;
          link.paidAt = new Date().toISOString().replace('T', ' ').slice(0, 19);
          await deliverWebhook(link);
        } else {
          link.status = 'CANCELLED';
          log.unshift(`Hủy link orderCode ${link.orderCode}`);
        }
      }
      res.writeHead(303, { location: '/dev' }); return res.end();
    }

    if (req.method === 'POST' && (path === '/dev/webhooks/on' || path === '/dev/webhooks/off')) {
      webhooksEnabled = path.endsWith('/on');
      res.writeHead(303, { location: '/dev' }); return res.end();
    }

    if (req.method === 'GET' && (path === '/' || path === '/dev')) {
      res.writeHead(200, { 'content-type': 'text/html; charset=utf-8' }); return res.end(devPage());
    }

    fail(res, '404', 'Không có endpoint này', 404);
  } catch (error) {
    fail(res, '500', error.message, 500);
  }
}).listen(PORT, () => console.log(`PayOS giả lập: ${BASE}/dev  (webhook -> ${WEBHOOK})`));
