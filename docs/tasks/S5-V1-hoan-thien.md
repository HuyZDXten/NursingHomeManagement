# S5-V1 · Hoàn thiện module Vận hành & Tài chính, slide, kịch bản demo

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Must | 5 | 5 (07–13/12) | mọi task Sprint 3–4 | `release/v1.0` |

## Mục tiêu

Đưa bản demo về trạng thái ổn định: không lỗi trên các luồng của Admin/Lễ tân/Kế toán/Khách, dữ liệu demo đẹp ở mọi màn, có slide và kịch bản demo 10 phút, merge `develop` → `main` và gắn tag `v1.0`.

## Việc cần làm

**Thứ Hai–Thứ Ba: QA bằng trình duyệt thật** (desktop + một màn hình tablet 768px), mỗi lỗi ghi vào bảng ở cuối file, sửa ngay lỗi chặn demo.

| Role | Màn hình cần đi hết | Kiểm |
| --- | --- | --- |
| Khách | Trang chủ, đặt tham quan, chatbot | Form lỗi rõ ràng, chatbot trả lời giá đúng |
| Receptionist | Tiếp nhận, cư dân (tạo/sửa/giường/family), phòng, lịch thăm, hóa đơn | Không thấy menu y tế; thử URL `/staff/medications` → trang 403 |
| Accountant | Hóa đơn (hàng loạt, PDF, VietQR), đối soát PayOS, báo cáo | Xuất Excel mở được |
| Admin | Tài khoản, ca trực, gói, dịch vụ phát sinh, tri thức AI, dashboard | Khóa tài khoản → người đó bị đăng xuất sau tối đa 30 phút |

**Thứ Tư: dữ liệu & kiểm thử**
- [ ] `05_DemoSeed.sql` mục 10: mọi màn của module có dữ liệu (biểu đồ không trống, có lịch tham quan ngày mai, có ca tuần này…).
- [ ] `.\Database\setup-local-db.ps1 -Recreate` trên máy **Huy** chạy sạch.
- [ ] `node scripts/smoke-test.mjs` xanh (gồm check của mọi task V).
- [ ] `dotnet build` 0 cảnh báo, `dotnet test`, `npm run build`, `npm run lint` sạch.
- [ ] Rà bảo mật: không còn secret trong `appsettings.json`/`.env` đã commit; đổi các secret từng lộ ở ASS2 nếu dùng lại tài khoản đó.

> **Đã chạy sớm 13/11 trên nhánh `feature/s4-v2-ai-handover` (cuối chuỗi task V):** mục 10 của seed có dữ liệu cho mọi màn module (10.1–10.6: tham quan ngày mai, ca tuần này, hóa đơn 2–4 tháng trước, nhật ký thuốc 7 ngày, đơn nghỉ + 2 bàn giao, dịch vụ phát sinh, cuộc trò chuyện) + tri thức AI ở `04`; smoke cô lập **213/213**; `dotnet build` API/Tests 0 cảnh báo; unit **87/87**; `npm run build`/`lint` sạch; không có `.env`, `appsettings.Development.json` hay key thật trong file đã commit. **Vẫn phải chạy lại** ở tuần 5 sau khi merge cả phần của Huy, và QA bằng trình duyệt (chưa ai xem các trang mới của Sprint 3–4 trên trình duyệt).

**Thứ Năm (11/12): đóng băng tính năng** — chỉ sửa lỗi. Merge `develop` → `main` bằng PR, Huy approve, `git tag v1.0`.

**Thứ Sáu (12/12): tổng duyệt** theo kịch bản dưới với Huy, bấm giờ.

## Slide (10–12 trang)

1. Bài toán & người dùng (8 actor) · 2. Kiến trúc 6 layer + React (hình ở `docs/ARCHITECTURE.md`) · 3. Database-first & ERD · 4. Phân quyền 2 lớp (policy + theo cư dân) · 5. Luồng nghiệp vụ chính · 6. PayOS 2 lớp xác nhận · 7. Realtime (SignalR) · 8. AI: chatbot, trợ lý gia đình, nhật ký, bàn giao ca + nguyên tắc an toàn · 9. Kiểm thử (unit + smoke) · 10. Phân công & Scrum (bảng `docs/tasks/README.md`) · 11. Hạn chế & hướng phát triển.

## Kịch bản demo 10 phút

| Phút | Người trình bày | Tài khoản | Thao tác |
| --- | --- | --- | --- |
| 0–1 | Viên | Khách | Trang chủ → hỏi chatbot "giá gói tiêu chuẩn?", "còn giường không?" → đặt tham quan |
| 1–2 | Viên | receptionist | Chuông báo yêu cầu mới → xác nhận tham quan → chuyển thành cư dân → phân giường → liên kết `family` |
| 2–3 | Viên | admin | Xếp ca tuần, phân công phụ trách |
| 3–5 | Huy | nurse, caregiver | Kế hoạch chăm sóc → "Sinh việc ngay" → caregiver cập nhật việc, ghi bữa ăn, báo sự cố mức Cao → nurse nhận realtime, đóng sự cố có báo gia đình |
| 5–6 | Huy | doctor, caregiver | Nhập thuốc trùng dị ứng → cảnh báo → caregiver phát thuốc |
| 6–8 | Viên | accountant, family2 | Hóa đơn hàng loạt → phát hành → `family2` nhận chuông → trả VietQR (payos-fake) → hóa đơn Paid → đối soát |
| 8–9 | Huy | family | Nhật ký AI đã duyệt, hỏi trợ lý "Hôm nay bố tôi thế nào?", đặt lịch thăm, nhắn tin nurse |
| 9–10 | Viên | admin | Dashboard + báo cáo + xuất Excel; tóm tắt kiến trúc |

Dự phòng: nếu mạng/AI lỗi khi demo, đổi `Ai:Provider` sang `Fake` và chiếu video ghi sẵn của Huy (S5-H1).

## Bảng lỗi QA

| # | Màn | Role | Mô tả | Mức | Người sửa | Trạng thái |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | | | | | | |
