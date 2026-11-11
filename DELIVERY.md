# Bàn giao Phase 2–6 và refactor Phase 1

Ngày: 11/11/2026 (Asia/Saigon).
Source: `D:\C#\NursingHomeManagement`.

## Kết quả triển khai

- Giữ một project ASP.NET Core MVC chính trong BE; frontend Razor/Bootstrap/Bootstrap Icons nằm cùng project. Không tạo SPA hoặc Web API riêng.
- Refactor Phase 1: Program dùng composition extension, account controller dễ đọc, DbContext chia cấu hình index/constraint/convention, seed chia theo trách nhiệm, định dạng C#/Razor/CSS/PowerShell và `.editorconfig` dùng chung.
- Controller chỉ nhận request, validation và gọi service; nghiệp vụ, authorization cấp đối tượng và transaction ở service; từng form có ViewModel để tránh overposting.

## Phase 2 — cư dân và phòng/giường

ResidentService, RoomService, LookupService; ResidentsController, RoomsController, BedsController;
form và view cho cư dân, người liên hệ, Family link, phòng, giường, phân/chuyển/trả giường và lịch sử.

Tìm kiếm/lọc/phân trang cư dân; ngừng lưu trú thay vì xóa. Giường có lịch sử không xóa;
giường bảo trì/phòng không hoạt động không được phân. Chuyển giường dùng transaction Serializable
và hai filtered unique index. Test thực tế gồm chuyển giường, chặn bảo trì và hai yêu cầu phân cùng giường đồng thời.

Kiểm tra tay: tạo phòng/giường, thêm cư dân, chuyển giường và xem lịch sử;
mở hai phiên để cùng phân một giường. Ngừng lưu trú và kiểm tra giường được trả.

## Phase 3 — sức khỏe và chăm sóc

HealthService, CareTaskService, HealthController, CareTasksController; các form/view tương ứng.
Admin tạo và phân công; Caregiver chỉ thấy cư dân được giao. Sinh hiệu kiểm tra giá trị và thời gian ghi UTC.
Cập nhật công việc cần kết quả/lý do, tạo CareRecord; rowversion chống ghi đè;
Completed/Cancelled giữ nguyên lịch sử. Ngừng lưu trú hủy công việc còn mở có lưu lý do.

Kiểm tra tay: Admin phân công, Caregiver ghi sinh hiệu/cập nhật công việc;
thử hồ sơ chưa được giao, số liệu sai và thiếu kết quả hoàn thành.

## Phase 4 — lịch thuốc

MedicationService/Controller, MedicationDay, MedicationForm/MedicationLogForm và view.
Admin nhập lịch đã phê duyệt; thay đổi bằng dừng lịch cũ và nhập lịch mới.
Caregiver chỉ ghi nhận, không thay đổi liều/lịch. Thuốc trong ngày có phân trang và lọc ngày.
Chặn trước giờ, ngoài thời hạn, lịch dừng, trùng lần dùng; Missed/Refused yêu cầu lý do.

Kiểm tra tay: nhập lịch giả lập đã phê duyệt, ghi Taken/Refused/Missed;
thử ghi trùng, thiếu lý do và trước giờ dự kiến.

## Phase 5 — billing, dashboard, Family

BillingService, DashboardService, FamilyService; controllers, ViewModels và Razor views.
Đăng ký gói không chồng ngày; giá thỏa thuận/snapshot; hóa đơn tháng tính theo ngày;
thêm phí khi Draft, phát hành khóa số tiền; thanh toán một phần/đủ, chống thanh toán vượt nợ và đồng thời;
hủy chỉ khi chưa có thanh toán, không xóa giao dịch qua ứng dụng.
Dashboard truy vấn dữ liệu SQL thật. Family được lọc theo liên kết, quyền nhóm và cờ chia sẻ từng bản ghi.
Bổ sung UserAccountService/UsersController và đổi mật khẩu.

Kiểm tra tay: đăng ký gói, tạo/phát hành hóa đơn, thanh toán từng phần;
sửa giá gói và kiểm tra hóa đơn cũ. Đăng nhập Family, thử cư dân khác và thay đổi quyền chia sẻ.

## Phase 6 — kiểm thử và bàn giao

- Build toàn bộ solution thực tế: 0 lỗi, 0 cảnh báo.
- xUnit/HTTP/SQL integration: 24/24 đạt, không bỏ qua test.
- Bao gồm chống CSRF, role redirect/logout, lưu form thật, render các module/form,
  chống phân giường trùng, chuyển giường, thanh toán đồng thời, snapshot hóa đơn,
  Family IDOR/chia sẻ từng bản ghi, Caregiver không tạo lịch thuốc, care history,
  medication validation, khóa tài khoản mất cookie, discharge và đổi mật khẩu.
- Migration AddWorkflowConstraints đã được áp dụng; model không có thay đổi chưa tạo migration.
- SQL connection đã được thực thi kiểm tra; database demo còn 1 cư dân và 4 role sau khi dọn dữ liệu do test tạo.
- Test fixture dùng database riêng NursingHomeTests_<GUID>, kiểm tra tên database trước seed và chỉ dọn database do fixture tạo.
- Có README, Start-Local.ps1, Test-Local.ps1, file inventory và tài khoản demo bàn giao riêng.

Lỗi cấu hình test host ban đầu đã được sửa; những hàng test do lần chạy đó tạo trong database demo
đã được xác định theo mã TEST- và dọn riêng, giữ dữ liệu seed. Các lượt kiểm thử sau dùng database test riêng.

## Phần chưa xác minh bằng mắt

Công cụ trình duyệt gặp ERR_CERT_AUTHORITY_INVALID trên HTTPS local, nên chưa kiểm tra trực quan
bố cục desktop/tablet. Không vượt cảnh báo chứng chỉ. Sau khi người dùng chạy
`dotnet dev-certs https --trust`, thực hiện checklist trình duyệt trong README.
Các trang/form đã qua kiểm tra render và HTTP tự động; đây không thay thế visual QA.

Không triển khai các mục ngoài phạm vi như cổng thanh toán, kê đơn, tự chẩn đoán,
hoàn tiền hoặc tác vụ tự động lập hóa đơn. Quy trình phê duyệt thuốc là tham chiếu từ bên ngoài hệ thống.
