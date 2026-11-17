# Hướng dẫn demo

Kịch bản 10 phút cho hội đồng, cộng các luồng bổ sung và **hai cách chạy thanh toán PayOS** (giả: an toàn, thật: có tiền thật).
Phân vai trình bày giữa Viên và Huy nằm ở bảng trong [tasks/S5-V1-hoan-thien.md](tasks/S5-V1-hoan-thien.md); tài liệu này nói rõ **bấm gì, thấy gì**.

## 1. Chuẩn bị (làm trước 30 phút, rồi tập lại một lần)

```powershell
.\scripts\demo.ps1 -Recreate        # dựng lại DB về dữ liệu demo sạch, bật PayOS giả + API + FE (mỗi thứ một cửa sổ)
```

| Tùy chọn | Ý nghĩa |
| --- | --- |
| `-Recreate` | Xóa và dựng lại DB `NursingHomeDB` từ `Database/*.sql`. **Mất mọi dữ liệu đang có** (kể cả tài khoản bạn tự tạo), nên chỉ dùng khi muốn trạng thái sạch. Bỏ cờ này để giữ dữ liệu. |
| `-PayOs Fake` (mặc định) / `-PayOs Real` | PayOS giả ở cổng 5199 hoặc PayOS thật (mục 4). |
| `-Ai Fake` | Chatbot trả lời theo kịch bản có sẵn, không gọi Gemini. Dùng làm dự phòng khi mất mạng. Mặc định dùng Gemini nếu đã có khóa. |
| `-Stop` | Tắt API, FE, PayOS giả. |

Script đặt chế độ PayOS **rõ ràng bằng biến môi trường** nên không bị lẫn khóa thật (trong user-secrets) với khóa demo (trong `appsettings.Development.json`). Đừng chạy `dotnet run` tay khi định demo PayOS giả: user-secrets sẽ ghi đè khóa demo bằng khóa thật và PayOS giả sẽ từ chối.

**Checklist trước khi vào phòng**
- [ ] Mở `http://localhost:5173`, đăng nhập `admin@nursinghome.local` / `Demo@123456`, thấy Dashboard có số liệu.
- [ ] Hỏi chatbot ở góc phải trang chủ: "giá gói tiêu chuẩn?" trả lời có số tiền.
- [ ] Mở sẵn 3 cửa sổ trình duyệt ẩn danh khác nhau cho 3 vai trò hay đổi qua lại (lễ tân, kế toán, gia đình) để khỏi đăng xuất/đăng nhập lúc demo.
- [ ] Mở `http://localhost:5199/dev` (PayOS giả) ở một tab riêng.
- [ ] Nếu muốn có đăng nhập Google: đã làm theo hướng dẫn tạo OAuth client, thêm Gmail của bạn vào Test users và tạo tài khoản dùng Gmail đó.

**Tài khoản** (mật khẩu chung `Demo@123456`): `admin`, `receptionist`, `accountant`, `doctor`, `nurse`, `caregiver`, `family`, `family2` + `@nursinghome.local`.
`family` là người thân của NH-0001, `family2` của NH-0002 (đều có quyền xem hóa đơn).

## 2. Kịch bản 10 phút

| Phút | Vai | Làm gì | Điểm cần nói |
| --- | --- | --- | --- |
| 0–1 | Khách (chưa đăng nhập) | Trang chủ → hỏi chatbot "giá gói tiêu chuẩn?", "còn giường không?" → bấm **Tin tức** → **Đặt lịch tham quan** (chọn ngày mai, buổi sáng, 2 khách) | Chatbot lấy giá và số giường trực tiếp từ dữ liệu, không bịa. Form báo lỗi rõ ràng, giới hạn chỗ mỗi buổi. |
| 1–2 | `receptionist` | Chuông báo "Lịch tham quan mới" → **Tiếp nhận & tham quan** → xác nhận lịch → chuyển thành cư dân → xếp giường → trong hồ sơ cư dân, tab **Tài khoản gia đình**: tạo mã mời | Thông báo realtime (SignalR). Giường đã có người thì hệ thống từ chối. Mã mời chỉ hiện một lần, hệ thống chỉ lưu bản băm. |
| 2–3 | `admin` | **Ca trực**: xếp ca tuần này, phân công người phụ trách cư dân. Mở **Nhân sự & chấm công**: hồ sơ, chứng chỉ sắp hết hạn | Cảnh báo chứng chỉ tự động, mỗi mốc báo một lần. |
| 3–5 | `nurse`, `caregiver` | Kế hoạch chăm sóc → "Sinh việc ngay" → caregiver cập nhật việc, ghi bữa ăn, báo sự cố mức Cao → nurse nhận thông báo, đóng sự cố có báo gia đình | Phân quyền theo cư dân: caregiver chỉ thấy cư dân mình phụ trách. |
| 5–6 | `doctor`, `caregiver` | Nhập thuốc trùng dị ứng → hệ thống cảnh báo → caregiver phát thuốc | Cảnh báo dị ứng; hệ thống không kê đơn hay chẩn đoán thay người. |
| 6–8 | `accountant`, `family2` | **Luồng thanh toán PayOS, xem mục 3** | Hai lớp xác nhận, không ghi tiền hai lần. |
| 8–9 | `family` | Nhật ký AI đã duyệt, hỏi trợ lý "Hôm nay bố tôi thế nào?", đặt lịch thăm, nhắn tin cho điều dưỡng | AI chỉ đọc dữ liệu được chia sẻ cho gia đình, từ chối tư vấn thuốc. |
| 9–10 | `admin` | **Báo cáo** (doanh thu, công nợ, sự cố, hiệu suất) → **Xuất Excel**. **Nhật ký thao tác**: thấy vừa làm gì. Tóm tắt kiến trúc (6 lớp, database-first, phân quyền 2 lớp) | Nhật ký không ghi mật khẩu/CCCD và không sửa/xóa được. |

**Luồng bổ sung** (nếu còn thời gian hoặc hội đồng hỏi): `nurse` → **Trang chủ của tôi** (Vào ca, việc hôm nay, trợ lý công việc) · `admin` → **Thông báo hàng loạt** (gửi cho vai trò Điều dưỡng, chuông của nurse nháy) · **Cấu hình & danh mục** (đổi sức chứa tham quan có hiệu lực ngay) · **Tin tức & album** (viết bài, đăng, xem ở `/tin-tuc`) · Hồ sơ cá nhân → kênh nhận email · đăng nhập Google.

## 3. Luồng thanh toán PayOS (giả) — khuyên dùng khi demo

Dữ liệu sẵn có sau `-Recreate`: hóa đơn **INV-202610-00002** của NH-0002 đang **Chưa thanh toán 8.000.000 đ**, `family2` là người thân.

1. **Kế toán** (`accountant`): **Hóa đơn** → mở INV-202610-00002 → cho thấy các khoản phí, trạng thái Phát hành. (Muốn làm từ đầu: **Tạo hóa đơn hàng loạt** → chọn kỳ tháng hiện tại → xem trước → tạo → **Phát hành**. Gia đình nhận chuông.)
2. **Gia đình** (`family2`, cửa sổ khác): chuông có thông báo hóa đơn → **Người thân** → hóa đơn → nút **Thanh toán 8.000.000 đ bằng VietQR**. Trang hiện mã QR, số tiền, ngân hàng, hạn 30 phút. Tải lại trang: vẫn đúng QR cũ.
3. **Tab PayOS giả** `http://localhost:5199/dev`: thấy link vừa tạo, bấm **Khách đã trả**. PayOS giả gửi webhook ký đúng chuẩn về API.
4. Trong vài giây trang của `family2` đổi sang **"Thanh toán thành công"** (trang hỏi trạng thái mỗi 5 giây). Chuông của gia đình và kế toán nháy. Hóa đơn chuyển **Đã thanh toán**.
5. **Kế toán** → **Đối soát PayOS**: dòng nhật ký webhook kết quả `APPLIED`. Nói thêm: nếu PayOS gửi trùng webhook, hệ thống ghi `DUPLICATE` và không cộng tiền lần hai vì mã tham chiếu `PAYOS-{orderCode}` là duy nhất ở DB (không bấm trùng được trên giao diện giả; đã kiểm bằng smoke test).

**Biến thể gây ấn tượng** (chọn một):
- *Mất webhook*: trước bước 3 bấm **Tắt webhook** ở tab PayOS giả, rồi **Khách đã trả**. Webhook không tới nhưng trong 5–20 giây hóa đơn vẫn **Đã thanh toán**, nhờ lớp 2 (trang gia đình và job đồng bộ hỏi lại PayOS). Bật webhook lại sau khi xong.
- *Webhook giả mạo*: gửi sai chữ ký, hệ thống ghi `REJECTED`, không ghi tiền:
  ```powershell
  curl -X POST http://localhost:5080/api/payments/payos/webhook -H "content-type: application/json" `
    -d '{"code":"00","desc":"success","data":{"orderCode":5001,"amount":1000,"reference":"FAKE"},"signature":"deadbeef"}'
  ```

## 4. Luồng thanh toán PayOS thật

Dùng kênh PayOS của bạn (3 khóa đã nằm trong user-secrets). **Tiền chuyển thật**, vào tài khoản ngân hàng gắn với kênh PayOS của bạn.

```powershell
.\scripts\demo.ps1 -PayOs Real
```

Số tiền hóa đơn demo là 8.000.000 đ nên **đừng trả đủ**. Giảm công nợ xuống số nhỏ bằng thu tiền thủ công rồi mới trả phần còn lại qua PayOS:

1. `accountant` → **Hóa đơn** → INV-202610-00002 → **Ghi nhận thanh toán thủ công**: số tiền `7.990.000`, hình thức Tiền mặt, mã phiếu thu `DEMO-TAY` (phải duy nhất). Hóa đơn chuyển **Trả một phần**, còn **10.000 đ**.
2. `family2` → hóa đơn → **Thanh toán 10.000 đ bằng VietQR**. Mã QR này do **PayOS thật** tạo.
3. Mở app ngân hàng, quét QR và chuyển 10.000 đ (không sửa số tiền hay nội dung).
4. Trong khoảng 5–20 giây trang tự báo **Thanh toán thành công** và hóa đơn **Đã thanh toán**. Không cần webhook vì lớp 2 hỏi PayOS.
5. `accountant` → **Đối soát PayOS** xem dòng `APPLIED`; vào my.payos.vn xem giao dịch/link thanh toán của kênh để đối chiếu.

**Webhook thật** (tùy chọn, để lớp 1 cũng chạy): `ngrok http 5080` (hoặc VS Dev Tunnels) → trên my.payos.vn mở kênh thanh toán, đặt Webhook URL `https://<địa chỉ tunnel>/api/payments/payos/webhook` → Lưu. PayOS gửi một webhook thử, hệ thống ghi `IGNORED` (đúng kỳ vọng).

**Lưu ý khi dùng PayOS thật**
- Chỉ nên làm khi có thời gian và mạng ổn; **trước hội đồng nên dùng PayOS giả**, hoặc quay sẵn video luồng thật làm dự phòng.
- Link hết hạn sau 30 phút. Hết hạn thì bấm thanh toán lại để tạo link mới.
- Báo "thanh toán đang bảo trì": `PayOs:Enabled` đang tắt hoặc khóa sai. Báo chữ ký/xác thực lỗi: sai `ChecksumKey` hoặc `ApiKey`.
- Sau khi thử, DB còn 10.000 đ đã trả. Muốn lặp lại bài thử thì chạy `.\scripts\demo.ps1 -Recreate -PayOs Real`.
- Khóa chỉ nằm trong user-secrets của máy bạn. Huy muốn thử thật phải tự đặt khóa trên máy mình, không đưa khóa vào file đã commit.

## 5. Dự phòng và xử lý sự cố khi demo

| Sự cố | Cách xử lý |
| --- | --- |
| Mất mạng, chatbot/trợ lý không trả lời | `.\scripts\demo.ps1 -Ai Fake` (chatbot trả theo kịch bản, câu hỏi giá/giường vẫn đúng) |
| Dữ liệu lộn xộn sau lần tập | `.\scripts\demo.ps1 -Recreate` |
| Cổng 5080/5173/5199 bị chiếm | `.\scripts\demo.ps1 -Stop` rồi chạy lại |
| Đăng nhập báo "quá nhiều lần" | Script đã nới giới hạn; nếu chạy tay, đặt `RateLimit__CredentialsPerMinute=300` |
| Đăng nhập Google báo `origin_mismatch` | Mở bằng `http://localhost:5173` (không phải `127.0.0.1`) và đã thêm origin trong Google Cloud Console |
| PayOS giả báo "khóa không đúng" | Đang chạy API tay với khóa thật trong user-secrets: chạy lại bằng `demo.ps1` |
| Mọi thứ hỏng | Chiếu video ghi sẵn (S5-H1) |

Kiểm tự động trước buổi demo: `.\scripts\run-smoke-isolated.ps1` (API, 492 kiểm tra, dùng DB riêng nên không đụng dữ liệu demo) và `node scripts/browser-qa/sweep.mjs` (giao diện, xem [browser-qa/README.md](../scripts/browser-qa/README.md)).
