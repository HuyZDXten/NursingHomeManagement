# BL-V5 · Hệ thống & truyền thông: nhật ký thao tác, cấu hình & danh mục, thông báo hàng loạt, tin tức & album (UC-13, UC-129, UC-130, UC-119, UC-120, UC-19, UC-115)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Could (backlog) | 14 | 17/11 | BL-V1 (upload ảnh), S3-V5 (email hóa đơn) | `feature/system-admin` |

## Mục tiêu

Admin xem được ai đã làm gì, chỉnh cấu hình cơ sở và danh mục không cần sửa code/khởi động lại; gửi thông báo cho cả nhóm người dùng; mỗi người chọn kênh email muốn nhận; cơ sở đăng tin tức và album ảnh công khai (có xác nhận đồng ý với album).

## Kết quả triển khai (17/11/2026)

- [x] **UC-13 nhật ký thao tác** (bảng `AuditLogs`, schema 9.11): thuộc tính `[Audit("Mã.thao-tác", "Loại đối tượng", "Mô tả")]` đặt trên action ghi dữ liệu quan trọng (khoảng 40 action: tài khoản, nhập viện, gói & hóa đơn, thanh toán tay, phòng & giường, hồ sơ lưu trú, tiền cọc, mã mời, hồ sơ & chứng chỉ nhân sự, sửa công, duyệt nghỉ phép, danh mục, thông báo, tin tức). Trang `/staff/audit` (Admin): lọc theo ngày, thao tác, từ khóa, người thực hiện; phân trang (tối đa 100 dòng).
    - **Chỉ ghi thao tác thành công** (2xx); thao tác bị từ chối hoặc lỗi không vào nhật ký. Ghi lỗi chỉ ghi log, không làm hỏng thao tác chính.
    - **Không ghi nội dung request** (mật khẩu, CCCD): chỉ phương thức, đường dẫn, người thực hiện (tên lúc ghi), IP, mã đối tượng. Không có endpoint sửa/xóa nhật ký.
    - Cấu hình cơ sở tự ghi nhật ký kèm giá trị trước/sau (trong `SettingsService`, không dùng `[Audit]`).
    - Chưa ghi: đăng nhập/đăng xuất và thao tác của module Chăm sóc (Huy có thể thêm `[Audit]` vào action của mình).
- [x] **UC-129 cấu hình cơ sở** (bảng `SystemSettings`): 12 khóa được phép sửa (tên, địa chỉ, hotline, giờ thăm, sức chứa tham quan/giờ thăm, 6 ngưỡng cảnh báo sinh hiệu), kiểm kiểu + khoảng giá trị + cặp ngưỡng (thấp < cao); **khóa bảo mật (JWT, SMTP, PayOS, Gemini) không sửa được từ giao diện**. Lưu cả nhóm trong một transaction.
    - `FacilitySettingsStore` thay `IOptions<FacilityOptions>`: mọi service đọc `facility.Value` ở mỗi request nên **giá trị mới có hiệu lực ngay**, không cần khởi động lại (đã kiểm: đổi sức chứa buổi tham quan 5 → 8 → 5). Nạp từ DB lúc API khởi động; lỗi DB thì dùng appsettings.
    - Giới hạn: chạy nhiều instance API thì instance khác chỉ nhận giá trị mới sau khi khởi động lại (đồ án chạy một instance).
- [x] **UC-130 danh mục** (bảng `Lookups`): 3 loại (quan hệ với cư dân, bộ phận, chức danh), Admin thêm/sửa/ẩn/xóa. Form mã mời, người liên hệ, hồ sơ nhân sự dùng làm **gợi ý** (`AutoComplete`, vẫn gõ được giá trị khác); tên lưu thẳng vào bản ghi dùng nó nên đổi hay xóa danh mục không làm hỏng dữ liệu cũ. "Loại sự cố", "loại giấy tờ" không đưa vào vì đã cố định bằng `CHECK` ở DB và gắn với logic.
- [x] **UC-119 thông báo hàng loạt** (bảng `Announcements`, trang `/staff/announcements`, Admin): gửi cho tất cả / gia đình / nhân viên / một vai trò. Luôn có thông báo trong ứng dụng (chuông + realtime); tick "gửi email" thì thêm email khi đã cấu hình SMTP, **tối đa 100 email mỗi lần** (gửi tuần tự trong request), người nhận chưa tắt kênh. Lưu số người nhận và số email đã gửi.
- [x] **UC-120 kênh nhận** (bảng `NotificationPreferences`, thẻ trong trang Hồ sơ): mỗi người bật/tắt email hóa đơn, nhắc thanh toán, thông báo chung. Chưa có dòng = bật hết. `InvoiceMailer` bỏ qua người đã tắt (hóa đơn phát hành → "hóa đơn", nhắc nợ → "nhắc thanh toán"); **chuông trong ứng dụng luôn gửi**, không tắt được. OTP quên mật khẩu không bị ảnh hưởng.
- [x] **UC-19/115 tin tức & album** (bảng `Posts`, `PostImages`): Admin và Lễ tân viết bài ở `/staff/posts` (nháp → đăng → gỡ, sửa khi đang đăng, ảnh tải qua `POST /api/uploads/image`, sắp thứ tự, chú thích). Trang công khai `/tin-tuc` (lọc tin tức/album, phân trang) và `/tin-tuc/:slug`, có nút "Tin tức" trên đầu trang công khai.
    - Nội dung là **chữ thuần** (FE không render HTML) nên không có XSS lưu trữ. Ảnh chỉ nhận URL do chính hệ thống lưu (`/uploads/...` hoặc `https://res.cloudinary.com/...`), chặn ảnh từ trang ngoài, `javascript:`, `..`.
    - **Album chỉ đăng khi đã tick xác nhận có sự đồng ý của cư dân/gia đình và có ít nhất một ảnh** (kiểm ở service và `CHECK` ở DB); album đang đăng không bỏ được xác nhận/xóa hết ảnh, phải gỡ trước. Không đổi loại bài sau khi tạo. Đường dẫn (slug) tự sinh từ tiêu đề (bỏ dấu), trùng thì thêm `-2`, **giữ nguyên khi sửa tiêu đề** để link đã chia sẻ không chết.
    - API công khai không trả người tạo, trạng thái nháp, xác nhận đồng ý.
- Seed: danh mục (9 quan hệ, 4 bộ phận, 6 chức danh), 2 bài tin tức đã đăng (không seed album vì cần xác nhận đồng ý).
- Kiểm thử: smoke thêm 76 kiểm tra (nhật ký đúng/không ghi khi lỗi/không lộ CCCD, cấu hình áp dụng ngay và hoàn tác, danh mục, thông báo theo nhóm, kênh nhận, tin tức/album đủ vòng đời + các ca chặn), unit thêm `SystemAdminTests` (73 test: catalog cấu hình, store, nhật ký, thông báo + lọc email, `InvoiceMailer` theo kênh, slug, URL ảnh, điều kiện đăng album).
- **Chưa xem trên trình duyệt. Email thật (SMTP) chưa thử** (smoke chạy khi chưa cấu hình SMTP, kiểm lọc người nhận bằng unit test).

## Cho Huy

- Muốn một thao tác của module Chăm sóc vào nhật ký: thêm `[Audit("Incident.Resolve", "Incident", "Xử lý sự cố")]` lên action (cần `using NursingHome.API.Filters;`), không cần sửa service.
- Ngưỡng sinh hiệu mặc định đọc từ `facility.Value.VitalAlerts` ở **mỗi lần dùng**; đừng lưu `.Value` vào trường của service singleton/job nền, nếu không sẽ không nhận giá trị Admin vừa đổi (service scoped đọc trong constructor thì vẫn đúng).
- Thông báo email mới của module Chăm sóc nên đi qua `ICommsRepository.ListOptedOutEmailsAsync(emails, EmailChannel.X)` trước khi gửi nếu muốn tôn trọng kênh nhận (kênh hiện có: Invoices, Reminders, Announcements).
