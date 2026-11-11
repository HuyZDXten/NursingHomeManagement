# Nursing Home Management System

Ứng dụng quản lý viện dưỡng lão cho nhóm 3 Fresher Developer. Đã triển khai phạm vi Phase 1–6 bằng **một ứng dụng ASP.NET Core MVC .NET 10**, Razor Views, Bootstrap 5, SQL Server, EF Core Code First và ASP.NET Core Identity.

## Chạy ứng dụng

Mở PowerShell tại `D:\C#\NursingHomeManagement`:

```powershell
dotnet tool restore
dotnet restore NursingHomeManagement.slnx
dotnet dev-certs https --trust
.\Start-Local.ps1
```

Script hỏi tên SQL Server và thông tin đăng nhập; không lưu mật khẩu SQL vào source. Máy hiện tại sử dụng `localhost` và SQL Authentication. Website chạy tại **https://localhost:7190**. Cần tin cậy chứng chỉ phát triển trước khi kiểm tra bằng trình duyệt.

Trên máy thành viên mới, khởi tạo database và demo:

```powershell
.\Start-Local.ps1 -Initialize
```

Login SQL phải có quyền tạo database hoặc quyền thao tác trên database do quản trị viên chuẩn bị. Demo chỉ được seed trong Development. Mật khẩu tài khoản mới tối thiểu 12 ký tự, có chữ hoa, chữ thường, chữ số và ký hiệu. Seed không đổi mật khẩu tài khoản đã tồn tại.

Các tài khoản demo: `admin@demo.nursinghome.test`, `receptionist@demo.nursinghome.test`, `caregiver@demo.nursinghome.test`, `family@demo.nursinghome.test`. Mật khẩu được bàn giao riêng trong file ngoài repository.

## Chức năng theo vai trò

- **Admin:** quản lý tài khoản/vai trò/trạng thái, cư dân, phòng/giường, gói dịch vụ, phân công chăm sóc, nhập lịch thuốc có tham chiếu phê duyệt, sinh hiệu, hóa đơn/thanh toán, dashboard.
- **Receptionist:** cư dân, liên hệ, liên kết Family, phòng/giường, đăng ký gói, tạo/phát hành hóa đơn và xem thanh toán. Không ghi nhận thanh toán, kê lịch thuốc hoặc xem sinh hiệu.
- **Caregiver:** chỉ xem cư dân có công việc Pending/InProgress được giao; ghi sinh hiệu, cập nhật công việc, ghi nhận thuốc. Không được sửa liều hoặc lịch đã phê duyệt, không hủy công việc.
- **Family:** chỉ đọc cư dân liên kết; từng nhóm sức khỏe/chăm sóc/thuốc/hóa đơn phụ thuộc quyền chia sẻ. Sức khỏe và chăm sóc còn phải được cho phép ở cấp bản ghi.

Tất cả tài khoản có đổi mật khẩu, logout POST và thông báo Access Denied. Thay đổi vai trò/khóa tài khoản làm cookie cũ mất hiệu lực ở request kế tiếp. Không có đăng ký công khai hoặc tự đăng ký Admin.

## Các luồng chính

### Cư dân, phòng và giường

1. Tạo cư dân trong **Hồ sơ cư dân**, điền mã, họ tên, ngày sinh và giới tính.
2. Thêm người liên hệ, liên kết tài khoản Family và chọn quyền chia sẻ. Nút **Sửa quyền** nạp lại các quyền hiện tại.
3. Tạo phòng, thêm giường. Từ hồ sơ chọn **Phân / chuyển giường**.
4. Khi chuyển, giường cũ được trả và giường mới được chiếm trong một transaction. Unique index phía SQL và transaction Serializable ngăn phân trùng.
5. Giường bảo trì/phòng ngừng sử dụng không được phân. Không xóa phòng có giường hoặc giường có lịch sử.
6. **Ngừng lưu trú** giữ hồ sơ, trả giường, dừng lịch thuốc, hủy công việc còn mở và lưu lý do trong CareRecord.

### Chăm sóc, sức khỏe, thuốc

Admin tạo công việc và chọn Caregiver. Caregiver ghi số liệu quan sát từ hồ sơ cư dân; hệ thống kiểm tra khoảng giá trị và tâm thu lớn hơn tâm trương. Các khoảng kiểm tra chỉ là validation kỹ thuật, không phải ngưỡng chẩn đoán.

Công việc đi từ Pending sang InProgress/Completed; chỉ Admin được Cancelled. Cần kết quả/lý do ở mỗi lần cập nhật. Mỗi lần cập nhật thêm CareRecord và dùng rowversion chống ghi đè. Completed/Cancelled không mở lại hoặc sửa lịch sử.

Lịch thuốc là lịch hàng ngày theo giờ Việt Nam, có khoảng ngày, liều/đường dùng và tham chiếu phê duyệt nhập bởi Admin. Muốn thay đổi: Admin dừng lịch cũ và nhập lịch mới theo phê duyệt mới, giữ nguyên lịch sử. Không có chức năng kê đơn hay đề xuất liều.

**Thuốc hôm nay** hiển thị Scheduled khi chưa có bản ghi sử dụng. Chỉ được ghi Taken/Missed/Refused khi đến giờ, trong khoảng ngày và cư dân đang lưu trú. Missed/Refused cần lý do. Một lịch + thời điểm chỉ có một log, không ghi đè. Missed không tự suy diễn: nhân viên ghi nhận rõ ràng.

### Gói dịch vụ, hóa đơn, thanh toán

- Mỗi cư dân có tối đa một gói trong cùng khoảng ngày; gói có giá thỏa thuận chốt tại lúc đăng ký.
- Đổi giá danh mục không đổi đăng ký đã chốt hoặc hóa đơn cũ. Đổi gói bằng cách kết thúc gói hiện tại và đăng ký gói mới từ ngày tiếp theo.
- Hóa đơn được tạo cho kỳ bắt đầu ngày 1. Tính phí theo số ngày giao nhau giữa tháng, gói đăng ký và thời gian lưu trú; ngày đầu/cuối đều được tính. Số ngày tháng là mẫu số.
- Làm tròn từng dòng tới 2 chữ số, midpoint away from zero. Hóa đơn lưu các dòng giá snapshot.
- **Draft:** được thêm khoản phí có nội dung, số lượng, đơn giá. **Phát hành:** khóa các dòng; chuyển Unpaid.
- Admin ghi nhận thanh toán thủ công với mã tham chiếu duy nhất. Thanh toán một phần => PartiallyPaid; đủ tiền => Paid.
- Số tiền phải dương, tối đa 2 chữ số thập phân và không vượt số còn nợ. Transaction bảo vệ thanh toán đồng thời.
- Chỉ Admin hủy hóa đơn chưa có thanh toán. Không xóa hóa đơn/thanh toán. Hóa đơn Cancelled vẫn chiếm kỳ định kỳ để ngăn tạo trùng; bản đầu không có quy trình thay thế/hoàn tiền.
- Dashboard: tổng cư dân/phòng/giường, trống/đang dùng, công việc hôm nay/chưa xong, tổng tiền thực thu và công nợ hóa đơn đã phát hành. Dữ liệu đều truy vấn SQL, không hardcode.

## Cấu trúc và quy ước clean code

```text
BE/
  Controllers/       nhận request, kiểm tra ModelState, gọi service
  Models/            16 entity nghiệp vụ + Identity user và enum
  ViewModels/        form riêng; không bind entity để ghi dữ liệu
  Services/          nghiệp vụ, quyền truy cập, transaction
  Data/              DbContext, seed an toàn, demo giả lập
  Extensions/        đăng ký DI, Identity, cookie, authorization
  Migrations/        InitialCreate + AddWorkflowConstraints + snapshot
  Views/             Razor theo module, editor template có kiểu dữ liệu
  wwwroot/           Bootstrap, CSS, JavaScript
Tests/               xUnit: HTTP integration + SQL integration
FE/README.md         vị trí giao diện trong ứng dụng MVC
```

Một trách nhiệm chính cho mỗi service; controller không tính phí, chuyển giường hoặc thao tác DbContext. Shared WorkflowController chỉ gom xử lý lỗi lưu dữ liệu, không chứa nghiệp vụ. CurrentUser/ResidentAccessService kiểm tra quyền ở service; Controller dùng Authorize độc lập. Không thêm repository/CQRS hoặc project kiến trúc trung gian.

Định dạng 4 dấu cách, tên có nghĩa, block nhiều dòng, khai báo form/entity rõ ràng. `.editorconfig` là quy ước dùng chung. Định dạng không cần MSBuild workspace:

```powershell
dotnet format whitespace BE --folder --exclude Migrations bin obj
dotnet format whitespace Tests --folder --exclude bin obj
```

Không sửa migration đã áp dụng để đổi schema. Thêm migration mới và commit cùng code.

## Database và cấu hình

16 bảng nghiệp vụ cùng các bảng Identity. FK Restrict để bảo toàn lịch sử; decimal(18,2) cho số tiền; DateOnly/TimeOnly cho ngày/giờ lịch; DateTimeOffset UTC cho sự kiện. UI hiển thị múi giờ UTC+7 của cơ sở.

```powershell
# Đặt ConnectionStrings__DefaultConnection qua biến môi trường, không commit secret.
dotnet ef migrations add TenThayDoi --project BE
dotnet run --project BE --no-launch-profile -- --initialize-db
dotnet run --project BE --no-launch-profile -- --check-db
dotnet ef migrations has-pending-model-changes --project BE
```

Không tự chạy migration khi người dùng mở trang. Development lưu DataProtection key mã hóa DPAPI (Windows) dưới `work/keys`, ngoài source và wwwroot. Production dùng key store do host cấu hình; chỉ triển khai với HTTPS và SQL certificate hợp lệ. `TrustServerCertificate=True` trong hướng dẫn dành cho local.

## Kiểm thử

```powershell
dotnet build NursingHomeManagement.slnx -m:1
# Nhập SQL login + mật khẩu demo khi script hỏi:
.\Test-Local.ps1
```

Bộ test gồm 24 test: login/logout/CSRF/role redirect, unique index SQL; render các module và form, POST cư dân, chuyển giường, phân giường đồng thời, care completion/history, medication log, snapshot giá/thanh toán một phần, Family object authorization/quyền chia sẻ, Caregiver không kê lịch thuốc, cookie khi khóa tài khoản, thanh toán đồng thời, ngừng lưu trú và đổi mật khẩu.

WorkflowFixture tạo database `NursingHomeTests_<GUID>` riêng, xác nhận tên connection trước khi seed, rồi chỉ xóa database test đó khi kết thúc. Test nền tảng kiểm tra database demo, các dữ liệu tạm dùng transaction rollback. Không chạy với dữ liệu thật/sản xuất.

Kết quả lần bàn giao và danh sách file theo phase: `DELIVERY.md` và `PHASE2-6-FILES.md`.

## Kiểm thử thủ công trước khi demo

1. Đăng nhập từng role; kiểm tra menu và truy cập URL không có quyền.
2. Admin tạo cư dân/phòng/giường, phân giường rồi chuyển. Mở 2 cửa sổ phân cùng một giường: chỉ một yêu cầu thành công.
3. Admin phân công; Caregiver ghi sinh hiệu, cập nhật công việc có ghi chú. Thử hồ sơ không được giao và giá trị sai.
4. Admin nhập lịch thuốc giả lập có tham chiếu; Caregiver ghi Taken/Refused/Missed, thử ghi trùng và ghi trước giờ.
5. Đăng ký gói, tạo hóa đơn tháng, thêm phí, phát hành. Admin thanh toán một phần rồi thanh toán hết. Thử vượt công nợ.
6. Sửa giá gói, xác nhận hóa đơn đã phát hành giữ nguyên. Kiểm tra dashboard thay đổi theo dữ liệu.
7. Family chỉ thấy người thân được liên kết; bật/tắt quyền và từng cờ ShareWithFamily để kiểm tra.
8. Khóa một tài khoản đang đăng nhập, xác nhận request tiếp theo yêu cầu đăng nhập lại.
9. Kiểm tra trình duyệt desktop/tablet, thông báo lỗi form, phím Tab và xác nhận thao tác.

## Git cho 3 thành viên

`main`: đã tích hợp và kiểm thử; `develop`: nhánh chung. Các nhánh:
`feature/resident-room`, `feature/health-care`, `feature/billing-dashboard`.
Mỗi người dùng SQL Server local, review migration trước merge. Không commit secret, MDF, BAK,
work/, bin/, obj/, chứng chỉ, khóa DataProtection hoặc tài khoản demo.

## Giới hạn rõ ràng

Đây là bản học tập quy mô nhỏ/trung bình, không phải phần mềm y tế được chứng nhận.
Không có cổng thanh toán, kê đơn, tự chẩn đoán, hoàn tiền hay tự động lập hóa đơn theo lịch.
Những mục này không thuộc phạm vi master prompt. Việc phê duyệt chuyên môn được tham chiếu từ bên ngoài hệ thống.

Trình duyệt tự động trong môi trường agent bị chặn bởi chứng chỉ HTTPS local chưa tin cậy, nên kiểm tra bố cục desktop/tablet bằng mắt còn cần thực hiện trên máy người dùng sau bước tin cậy chứng chỉ. Các trang/form đã được render và kiểm tra HTTP tự động.
