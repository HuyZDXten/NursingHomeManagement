# Kế hoạch Scrum — Nursing Home Management

Dựa trên báo cáo phân tích (142 use case: UC-01..UC-130 + AI-01..AI-12). 6 sprint trong 8 tuần (Sprint 3–5 do Viên và Huy thực hiện, xem [tasks/README.md](tasks/README.md)), làm hết **Must** trước rồi mới tới Should/Could. Mỗi sprint kết thúc bằng một bản chạy được và `Database/` được cập nhật.

**Trạng thái tại nhánh `feature/v2-api-react` (13/11/2026): Sprint 0, 1, 2 xong — 56 use case (một số làm một phần, ghi rõ bên dưới). Còn 86 use case cho Sprint 3–5.**

## Definition of Done (áp dụng cho mọi use case)

- [ ] Bảng/cột mới đã thêm vào `Database/01_Schema.sql` + Entity + mapping (`NursingHomeContext`), có dữ liệu demo trong `05_DemoSeed.sql`.
- [ ] Đủ đường đi: DAO → Repository → Service (kiểm tra quyền + quy tắc) → DTO → Controller (policy) → FE (`api/`, trang, route, menu).
- [ ] Quyền theo từng cư dân dùng `IResidentAccessService` (không chỉ dựa vào policy).
- [ ] `dotnet build` 0 lỗi 0 cảnh báo, `dotnet test` xanh, `npm run build` và `npm run lint` sạch.
- [ ] Thử tay bằng tài khoản demo của từng role liên quan, kể cả role KHÔNG được phép.
- [ ] Cập nhật bảng trạng thái trong file này.

## Lịch sprint

| Sprint | Thời gian | Mục tiêu | Trạng thái |
| --- | --- | --- | --- |
| 0 | Tuần 1 | Nền tảng: solution 6 layer, `Database/`, JWT, FE khung | Xong |
| 1 | Tuần 2–3 | Chuyển 40 use case của bản MVC sang API + React | Xong |
| 2 | Tuần 4 | PayOS VietQR, đối soát, thông báo realtime | Xong |
| 3 | 16–29/11 | Module mới: ca trực, sự cố, ADL, lịch thăm, chat, báo cáo... | Chưa làm |
| 4 | 30/11–06/12 | AI: chatbot tư vấn, trợ lý gia đình, tóm tắt | Chưa làm |
| 5 | 07–13/12 | Hoàn thiện: test, seed demo, tài liệu, slide, tổng duyệt | Chưa làm |

Mốc an toàn: hết Sprint 2 đã đủ để demo (bản cũ chạy trên API + React + PayOS).

---

## Sprint 0 — Nền tảng (Xong)

| UC | Nội dung | Trạng thái |
| --- | --- | --- |
| — | Solution `BE/NursingHome.sln` 6 project + Tests, `Directory.Build.props`, DI `AddNursingHomeServices` | Xong |
| — | `Database/` 00–05, `setup-local-db.ps1`, `upgrades/`, bỏ EF Migrations & seed C# | Xong |
| — | FE Vite + React + TS + antd, router, `AuthContext`, layout + menu theo role | Xong |
| UC-01, 03, 04 | Đăng nhập JWT, refresh token xoay vòng (phát hiện dùng lại), đăng xuất | Xong |
| UC-05 | Quên mật khẩu bằng OTP email (cần cấu hình `Email`) | Xong |
| UC-06 | Đổi mật khẩu (thu hồi phiên ở thiết bị khác) | Xong |
| UC-07 | Hồ sơ cá nhân | Một phần — chưa có ảnh đại diện (Cloudinary) |
| UC-09..12 | Admin: tạo/sửa/khóa tài khoản, đổi role, tìm kiếm OData, đặt lại mật khẩu | Xong |
| UC-14..16 | Trang công khai: giới thiệu, bảng giá gói, số giường trống | Xong |
| UC-02 | Đăng nhập Google | Chuyển Sprint 3 |
| UC-08 | Kích hoạt tài khoản Family bằng mã mời | Chuyển Sprint 3 |
| UC-13 | Audit log | Chuyển Sprint 3 |

## Sprint 1 — Chuyển bản MVC (Xong)

| UC | Nội dung | Trạng thái |
| --- | --- | --- |
| UC-23..28 | Tiếp nhận, sửa, tìm kiếm (OData), hồ sơ 360°, người liên hệ, liên kết Family + 4 quyền | Xong (UC-24 chưa upload ảnh) |
| UC-32 | Ngừng lưu trú: trả giường, dừng thuốc, hủy việc mở (transaction Serializable) | Xong |
| UC-34, 35 | CRUD phòng (có tầng), giường, bảo trì | Xong (loại phòng = UC-36) |
| UC-37..40 | Phân/chuyển/trả giường (filtered unique index), lịch sử, sơ đồ phòng theo tầng | Xong |
| UC-50, 52..55 | Giao việc (Admin/Nurse), việc của tôi, cập nhật có rowversion, hủy (Admin), lịch sử | Xong |
| UC-62, 63 | Ghi sinh hiệu, biểu đồ theo thời gian | Xong |
| UC-65 | Cảnh báo khi vượt ngưỡng (ngưỡng mặc định ở `Facility:VitalAlerts`) | Xong (ngưỡng riêng từng cư dân = UC-64) |
| UC-73, 75..78 | Lịch thuốc (Doctor/Admin), dừng lịch, bảng phát thuốc, ghi Taken/Missed/Refused, lịch sử | Xong |
| UC-83, 84 | Gói dịch vụ, đăng ký gói giá chốt, kết thúc gói | Xong |
| UC-87, 89..92 | Hóa đơn tháng tính theo ngày, thêm phí khi Draft, phát hành, hủy, thu tiền thủ công | Xong |
| UC-100..102 | Cổng Family: người thân, sức khỏe/chăm sóc/thuốc/hóa đơn theo quyền | Xong |
| UC-121 | Dashboard (SQL thật) + biểu đồ thực thu 6 tháng | Xong |

## Sprint 2 — Thanh toán & thông báo (Xong)

| UC | Nội dung | Trạng thái |
| --- | --- | --- |
| UC-93 | Family thanh toán VietQR PayOS (QR trên trang, poll 5 giây) | Xong |
| UC-94 | Lễ tân/kế toán tạo VietQR tại quầy | Xong |
| UC-95 | Webhook PayOS (kiểm chữ ký, chống ghi trùng) + đồng bộ nền (lớp 2) | Xong |
| UC-96 | Màn đối soát: giao dịch, nhật ký báo có, đồng bộ tay | Xong |
| UC-117 | Trung tâm thông báo + SignalR realtime | Xong |
| UC-107 | Family nhận thông báo hóa đơn phát hành, đã thanh toán | Một phần (sự cố, sinh hiệu cho Family: Sprint 3) |
| UC-118 | Email theo sự kiện | Một phần — mới có OTP; email hóa đơn/lịch thăm: Sprint 3 |
| — | `scripts/payos-fake` (PayOS giả lập), `scripts/smoke-test.mjs` (31 kiểm tra) | Xong |

---

## Sprint 3–5 — Viên & Huy (kế hoạch mới)

Từ Sprint 3, nhóm còn **2 người**: **Viên** phụ trách module *Vận hành & Tài chính*, **Huy** phụ trách module *Chăm sóc & Gia đình*. Mỗi task có file hướng dẫn chi tiết (DDL, file cần tạo, API, màn hình, kiểm thử). **Bảng task và trạng thái cập nhật ở [tasks/README.md](tasks/README.md)**; file này chỉ giữ tóm tắt.

| Sprint | Thời gian | Viên | Huy |
| --- | --- | --- | --- |
| 3 tuần 1 (Must) | 16–22/11 | [S3-V1](tasks/S3-V1-tiep-nhan-tham-quan.md) Tiếp nhận & tham quan · [S3-V2](tasks/S3-V2-ca-truc-phu-trach.md) Ca trực & phụ trách · [S3-V3](tasks/S3-V3-bao-cao.md) Báo cáo (doanh thu, công suất) | [S3-H1](tasks/S3-H1-su-co.md) Sự cố · [S3-H2](tasks/S3-H2-benh-nen-di-ung-che-do-an.md) Bệnh nền, dị ứng, chế độ ăn · [S3-H3](tasks/S3-H3-lich-tham.md) Lịch thăm |
| 3 tuần 2 (Should) | 23–29/11 | S3-V3 (thuốc, sự cố) · [S3-V4](tasks/S3-V4-ban-giao-ca-nghi-phep.md) Bàn giao ca & nghỉ phép · [S3-V5](tasks/S3-V5-tai-chinh-mo-rong.md) Tài chính mở rộng · [S3-V6](tasks/S3-V6-chat-gia-dinh.md) Chat gia đình | [S3-H4](tasks/S3-H4-ke-hoach-cham-soc-adl-bua-an.md) Kế hoạch chăm sóc, ADL, bữa ăn · [S3-H5](tasks/S3-H5-danh-gia-phieu-kham-nguong.md) Đánh giá, phiếu khám, ngưỡng · [S3-H6](tasks/S3-H6-nhat-ky-phan-hoi-gia-dinh.md) Nhật ký & phản hồi gia đình |
| 4 (AI) | 30/11–06/12 | [S4-V1](tasks/S4-V1-ai-ha-tang-chatbot.md) Hạ tầng AI + chatbot tư vấn · [S4-V2](tasks/S4-V2-ai-ban-giao-ca-bao-cao.md) AI bàn giao ca | [S4-H1](tasks/S4-H1-ai-tro-ly-gia-dinh.md) Trợ lý gia đình · [S4-H2](tasks/S4-H2-ai-nhat-ky-sinh-hieu.md) Nhật ký AI + xu hướng sinh hiệu |
| 5 (hoàn thiện) | 07–13/12 | [S5-V1](tasks/S5-V1-hoan-thien.md) QA, slide, kịch bản demo, release | [S5-H1](tasks/S5-H1-hoan-thien.md) QA, kiểm thử, video dự phòng |

Use case còn lại (Could và một phần Should) đã chia sẵn người làm ở [tasks/BACKLOG.md](tasks/BACKLOG.md), chỉ lấy khi task chính đã merge.

Nguyên tắc AI (áp dụng mọi task Sprint 4): không chẩn đoán, không đề xuất liều; dữ liệu AI tạo chỉ là bản nháp có người duyệt; tool AI luôn đi qua service để giữ phân quyền; không gửi CCCD/BHYT/SĐT ra ngoài; rate limit `RateLimit:AiMessagesPerMinute`.

Nghi thức Scrum cho 2 người, quy trình làm task, review chéo, quyền sở hữu file: [tasks/README.md](tasks/README.md).
