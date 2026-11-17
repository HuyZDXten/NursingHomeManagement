# Hướng dẫn demo

Kịch bản 10 phút cho hội đồng, kèm luồng thanh toán PayOS. Phân vai trình bày giữa Viên và Huy ở [tasks/S5-V1-hoan-thien.md](tasks/S5-V1-hoan-thien.md); tài liệu này nói rõ **bấm gì, thấy gì**.

## 1. Chạy hệ thống

**Cách đơn giản (hai terminal):**

```powershell
# terminal 1 — API (http://localhost:5080)
cd BE\NursingHome.API
dotnet run

# terminal 2 — giao diện (http://localhost:5173)
cd FE
npm run dev
```

Cấu hình lấy từ `appsettings*.json` và user-secrets của máy (xem HANDOVER "Khóa thật"). Trên máy Viên: **PayOS thật**, Gemini thật, Gmail SMTP thật, Cloudinary thật.

**Đưa dữ liệu về trạng thái sạch** (nên làm trước mỗi lần tập và trước buổi demo chính thức). Tắt API (`Ctrl+C`) rồi:

```powershell
.\Database\setup-local-db.ps1 -Recreate     # XÓA và dựng lại DB: mất mọi dữ liệu đang có, kể cả tài khoản bạn tự tạo
cd BE\NursingHome.API; dotnet run
```

**Script thay thế** (mỗi thứ một cửa sổ, đặt chế độ PayOS bằng biến môi trường):

```powershell
.\scripts\demo.ps1 -Recreate            # dựng lại DB + PayOS GIẢ + API + FE
.\scripts\demo.ps1 -PayOs Real          # PayOS thật
.\scripts\demo.ps1 -Ai Fake             # chatbot trả lời theo kịch bản, không gọi Gemini (dự phòng khi mất mạng)
.\scripts\demo.ps1 -Stop                # tắt hết
```

Dùng `demo.ps1 -PayOs Fake` khi cần PayOS giả: user-secrets chứa khóa thật nên chạy `dotnet run` tay sẽ luôn là PayOS thật.

## 2. Chuẩn bị trước giờ demo

- [ ] SQL Server đang chạy; mở `http://localhost:5173` thấy trang chủ; đăng nhập `admin@nursinghome.local` / `Demo@123456` thấy Dashboard có số liệu.
- [ ] Hỏi chatbot ở góc phải trang chủ "giá gói tiêu chuẩn?" trả lời có số tiền.
- [ ] **Hai trình duyệt, không phải hai cửa sổ ẩn danh.** Phiên đăng nhập lưu trong localStorage theo địa chỉ web, nên mọi cửa sổ cùng trình duyệt (và mọi cửa sổ ẩn danh cùng nhau) dùng chung một phiên: đăng nhập vai trò khác ở cửa sổ này sẽ đè phiên ở cửa sổ kia. Dùng **Chrome cho nhân viên** (đổi vai trò bằng Đăng xuất/Đăng nhập) và **Edge cho khách/gia đình**, để thấy thông báo realtime giữa hai bên.
- [ ] Đã tập PayOS một lần (mục 4), sau đó dựng lại DB sạch.
- [ ] Email: hệ thống gửi email thật qua Gmail khi tạo mã mời hoặc phát hành hóa đơn tới địa chỉ có thật. Tài khoản demo dùng `@nursinghome.local` (không tồn tại) nên email không tới. Khi demo mã mời, nhập email của chính bạn.
- [ ] Đăng nhập Google (tùy chọn): Gmail của bạn nằm trong Test users, đã tạo tài khoản dùng Gmail đó (sau `-Recreate` phải tạo lại).

**Tài khoản** (mật khẩu chung `Demo@123456`): `admin`, `receptionist`, `accountant`, `doctor`, `nurse`, `caregiver`, `family`, `family2`, cộng `@nursinghome.local`. `family` là người thân của NH-0001, `family2` của NH-0002 (đều xem được hóa đơn).

## 3. Kịch bản 10 phút

| Phút | Vai (trình duyệt) | Làm gì | Điểm cần nói |
| --- | --- | --- | --- |
| 0–1 | Khách (Edge) | Trang chủ → hỏi chatbot "giá gói tiêu chuẩn?", "còn giường không?" → **Tin tức** → **Đặt lịch tham quan** (ngày mai, buổi sáng, 2 khách) | Chatbot lấy giá và số giường trực tiếp từ dữ liệu. Giới hạn số khách mỗi buổi. |
| 1–2 | `receptionist` (Chrome) | Chuông báo "Lịch tham quan mới" → **Tiếp nhận & tham quan** → tab *Lịch tham quan*: **Xác nhận**. Rồi lấy yêu cầu có sẵn **Huỳnh Quốc Bảo** (đã tham quan, quan tâm Gói Cao cấp) ở tab *Yêu cầu tư vấn* → **Nhập viện** (tạo hồ sơ cư dân) → trong hồ sơ cư dân: tab **Giường** → **Phân giường**; tab **Gói & hóa đơn** → **Đăng ký gói** (xem mục 3a); tab **Tài khoản gia đình** → tạo mã mời | Thông báo realtime. Giường đã có người thì bị từ chối. Mã mời chỉ hiện một lần, DB chỉ lưu bản băm. Lịch tham quan mới đặt chỉ "Hoàn thành" được từ ngày tham quan, nên dùng yêu cầu có sẵn để đi tiếp tới nhập viện. |
| 2–3 | `admin` | **Ca trực**: tab *Lịch ca tuần* xếp ca, tab *Phân công phụ trách*. Mở **Nhân sự & chấm công**: hồ sơ, chứng chỉ sắp hết hạn | Cảnh báo chứng chỉ tự động, mỗi mốc báo một lần. |
| 3–5 | `nurse`, `caregiver` | Cư dân → tab **Kế hoạch chăm sóc** → **Sinh việc ngay** → đăng nhập `caregiver` cập nhật việc, ghi bữa ăn, tab **Sự cố** → **Báo sự cố** mức Cao → `nurse` nhận thông báo, đóng sự cố có báo gia đình | Phân quyền theo cư dân: caregiver chỉ thấy cư dân mình phụ trách. |
| 5–6 | `doctor`, `caregiver` | Tab **Thuốc**: nhập thuốc trùng dị ứng đã ghi → cảnh báo → `caregiver` **Phát thuốc** | Cảnh báo dị ứng; hệ thống không kê đơn hay chẩn đoán thay người. |
| 6–8 | `accountant`, `family2` (Edge) | **Luồng thanh toán PayOS, mục 4** | Hai lớp xác nhận, không ghi tiền hai lần. |
| 8–9 | `family` (Edge) | Nhật ký AI đã duyệt, hỏi trợ lý "Hôm nay bố tôi thế nào?", đặt lịch thăm, nhắn tin cho điều dưỡng | AI chỉ đọc dữ liệu được chia sẻ cho gia đình, từ chối tư vấn thuốc. |
| 9–10 | `admin` | **Báo cáo** (doanh thu, công nợ, sự cố, hiệu suất) → **Xuất Excel**. **Nhật ký thao tác** thấy những gì vừa làm. Tóm tắt kiến trúc (6 lớp, database-first, phân quyền 2 lớp) | Nhật ký không ghi mật khẩu/CCCD và không sửa/xóa được. |

### 3a. Gói dịch vụ: đăng ký thế nào, vì sao cư dân mẫu đã có sẵn gói

- Gói gắn với **cư dân**, không gắn với tài khoản gia đình. Tài khoản `family` chỉ là người thân liên kết với cư dân và xem hóa đơn của cư dân; gia đình không chọn gói, chỉ thấy tên gói trong dòng phí của hóa đơn.
- Các cư dân mẫu **có sẵn gói do dữ liệu demo**: NH-0001 Tiêu chuẩn 12 triệu, NH-0002 Cơ bản 8 triệu, NH-0003 Cao cấp 18 triệu, NH-0004 Tiêu chuẩn, NH-0005 Cơ bản (đã ngừng). Cư dân tạo mới **chưa có gói nào**: nhập viện chỉ tạo hồ sơ, giường và gói là hai bước làm tiếp.
- **Đăng ký:** Admin, Lễ tân hoặc Kế toán → **Cư dân** → mở hồ sơ → tab **Gói & hóa đơn** → **Đăng ký gói** → chọn gói (hiện kèm giá), ngày bắt đầu, ngày kết thúc (bỏ trống nếu chưa biết) → Lưu.
- **Quy tắc:** chỉ cư dân đang lưu trú và gói đang hoạt động. Mỗi thời điểm chỉ một gói: đổi gói thì bấm **Kết thúc** gói cũ rồi đăng ký gói mới. Giá được **chốt lúc đăng ký**, Admin đổi giá gói sau đó không ảnh hưởng cư dân đã đăng ký.
- **Hóa đơn tháng** tính theo số ngày gói có hiệu lực trong kỳ (cộng phụ thu loại phòng và dịch vụ phát sinh). Cư dân chưa có gói thì không tạo được hóa đơn ("Không có đăng ký gói và ngày lưu trú phù hợp trong kỳ này") và hóa đơn hàng loạt bỏ qua họ.
- Danh mục gói (tên, giá, mô tả, ẩn/hiện) do Admin sửa ở menu **Gói dịch vụ**.

**Luồng bổ sung** (nếu còn thời gian hoặc hội đồng hỏi): `nurse` → **Trang chủ của tôi** (Vào ca, việc hôm nay, trợ lý công việc) · `admin` → **Thông báo hàng loạt** (gửi cho vai trò Điều dưỡng, chuông của nurse nháy) · **Cấu hình & danh mục** (đổi sức chứa tham quan có hiệu lực ngay) · **Tin tức & album** (viết bài, đăng, xem ở `/tin-tuc`) · Hồ sơ cá nhân → kênh nhận email · **Ca trực → Bàn giao ca → Viết tóm tắt bằng AI** · đăng nhập Google.

## 4. Luồng thanh toán PayOS

Dữ liệu sau khi dựng DB sạch: hóa đơn **INV-202610-00002** của NH-0002 đang **Chưa thanh toán 8.000.000 đ**, `family2` là người thân. Với PayOS thật, **tiền chuyển thật** vào tài khoản ngân hàng gắn với kênh PayOS của bạn, nên không trả đủ 8 triệu: thu bớt bằng tay rồi trả phần còn lại qua PayOS.

1. **Kế toán** (`accountant`): **Hóa đơn** → mở INV-202610-00002, cho xem các khoản phí.
2. **Ghi nhận thanh toán thủ công**: số tiền `7.990.000`, hình thức Tiền mặt, mã phiếu thu `DEMO-TAY` (phải duy nhất). Hóa đơn chuyển **Trả một phần**, còn **10.000 đ**.
3. **Gia đình** (`family2`, Edge): chuông có thông báo → **Người thân** → hóa đơn → nút **Thanh toán 10.000 đ bằng VietQR**. Hiện mã QR do PayOS tạo, số tiền, ngân hàng, hạn 30 phút. Tải lại trang vẫn đúng QR cũ.
4. Mở app ngân hàng trên điện thoại, **quét QR và chuyển 10.000 đ** (không sửa số tiền hay nội dung).
5. Trong 5–20 giây trang gia đình tự đổi sang **"Thanh toán thành công"**, hóa đơn thành **Đã thanh toán**, chuông của gia đình và kế toán nháy.
6. **Kế toán** → **Đối soát PayOS**: dòng nhật ký `APPLIED` (có nút hỏi PayOS để đồng bộ tay khi cần). Có thể mở my.payos.vn đối chiếu giao dịch của kênh.

Nói với hội đồng: có **hai lớp xác nhận**. Lớp 1 là webhook PayOS gọi về, kiểm chữ ký. Lớp 2 là trang gia đình (5 giây) và job nền hỏi lại PayOS, nên mất webhook vẫn ghi nhận. Cả hai đi qua một hàm ghi trong transaction và mã tham chiếu `PAYOS-{orderCode}` là duy nhất ở DB, nên không cộng tiền hai lần. Local không có webhook (cần địa chỉ công khai) nên bước 5 là lớp 2 làm việc.

**Webhook thật** (tùy chọn): `ngrok http 5080` rồi đặt Webhook URL `https://<địa chỉ tunnel>/api/payments/payos/webhook` trên my.payos.vn. PayOS gửi một webhook thử, hệ thống ghi `IGNORED` (đúng kỳ vọng).

**PayOS giả** (không tốn tiền, kiểm soát được từng bước): chạy `.\scripts\demo.ps1 -PayOs Fake`, mở `http://localhost:5199/dev`. Thay bước 4 bằng bấm **Khách đã trả** ở tab đó (thanh toán cả 8.000.000 đ được, không cần ghi thu tay trước). Hai biến thể gây ấn tượng: bấm **Tắt webhook** rồi **Khách đã trả** (hóa đơn vẫn Đã thanh toán sau 5–20 giây nhờ lớp 2); hoặc gửi webhook sai chữ ký, hệ thống ghi `REJECTED` và không ghi tiền:

```powershell
curl -X POST http://localhost:5080/api/payments/payos/webhook -H "content-type: application/json" `
  -d '{"code":"00","desc":"success","data":{"orderCode":5001,"amount":1000,"reference":"FAKE"},"signature":"deadbeef"}'
```

**Lưu ý với PayOS thật**
- Cần mạng ổn. Trước hội đồng nên có phương án dự phòng: PayOS giả, hoặc video quay sẵn luồng thật.
- Link hết hạn sau 30 phút; hết hạn thì bấm thanh toán lại.
- Báo "thanh toán đang bảo trì": `PayOs:Enabled` tắt hoặc khóa sai. Báo lỗi chữ ký/xác thực: sai `ChecksumKey` hoặc `ApiKey`.
- Mỗi lần tập xong có 10.000 đ đã trả: dựng lại DB sạch để về 8.000.000 đ.
- Khóa PayOS chỉ nằm trong user-secrets của máy; Huy muốn thử thật phải tự đặt khóa trên máy mình, không đưa vào file đã commit.

## 5. Dự phòng và xử lý sự cố

| Sự cố | Cách xử lý |
| --- | --- |
| Mất mạng, chatbot/trợ lý không trả lời | `.\scripts\demo.ps1 -Ai Fake` (giá/giường vẫn đúng) |
| PayOS thật không lên | `.\scripts\demo.ps1 -PayOs Fake` hoặc chiếu video |
| Dữ liệu lộn xộn sau lần tập | Tắt API, `.\Database\setup-local-db.ps1 -Recreate`, chạy lại |
| Cổng 5080/5173 bị chiếm | `.\scripts\demo.ps1 -Stop` |
| Đổi vai trò mà vẫn thấy vai cũ | Cùng trình duyệt dùng chung phiên: Đăng xuất trước, hoặc dùng trình duyệt thứ hai |
| Đăng nhập báo "quá nhiều lần" | `dotnet user-secrets set "RateLimit:CredentialsPerMinute" "300" --id nursinghome-api-prn232` rồi chạy lại API |
| Đăng nhập Google báo `origin_mismatch` hoặc "Access blocked" | Mở bằng `http://localhost:5173` (không phải `127.0.0.1`); Gmail phải nằm trong Test users; Client ID đúng ở `appsettings.json` và user-secrets không còn `Google:ClientId` cũ |
| Mọi thứ hỏng | Chiếu video ghi sẵn (S5-H1) |

Kiểm tự động trước buổi demo: `.\scripts\run-smoke-isolated.ps1` (API, 492 kiểm tra, dùng DB riêng nên không đụng dữ liệu demo) và `node scripts/browser-qa/sweep.mjs` (giao diện, xem [browser-qa/README.md](../scripts/browser-qa/README.md)).
