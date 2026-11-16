# Database — NursingHomeDB (database-first)

Thư mục này là **nguồn sự thật duy nhất** của schema và dữ liệu mẫu. API không tạo, không migrate DB; `NursingHomeContext` chỉ mapping. Không còn EF Migrations và không seed bằng C#.

## Chạy

```powershell
# Từ thư mục gốc repo, SQL Server local dùng Windows Authentication
.\Database\setup-local-db.ps1              # tạo nếu chưa có, giữ dữ liệu đang có
.\Database\setup-local-db.ps1 -Recreate    # xóa và dựng lại từ đầu (dừng API trước)
.\Database\setup-local-db.ps1 -SkipDemoSeed
.\Database\setup-local-db.ps1 -Server ".\SQLEXPRESS" -SqlUser sa -SqlPassword "..."
.\Database\setup-local-db.ps1 -Database NursingHomeDB_Test -Recreate   # DB riêng để test, không đụng DB dev
```

Cần `sqlcmd` (đi kèm SQL Server Management Studio hoặc gói *Microsoft Command Line Utilities*).

### Một file duy nhất (SSMS, không cần PowerShell)

`NursingHomeDB_AllInOne.sql` gộp `00` → `05` thành một file, tạo đúng database như `setup-local-db.ps1`: collation `Vietnamese_CI_AS`, `READ_COMMITTED_SNAPSHOT`, đủ bảng, view, thủ tục, danh mục và dữ liệu demo.

- **SSMS:** File → Open → `Database\NursingHomeDB_AllInOne.sql` → Execute (F5). Không cần bật SQLCMD Mode. Tab Messages kết thúc bằng `NursingHomeDB_AllInOne.sql: xong.` và không có dòng đỏ là xong.
- **sqlcmd:** `sqlcmd -S localhost -E -C -b -I -x -f 65001 -i Database\NursingHomeDB_AllInOne.sql`
- **Chạy lại nhiều lần được.** DB cũ (ví dụ chỉ có 21 bảng Sprint 0–2) chạy file này sẽ được thêm bảng mới, dữ liệu cũ giữ nguyên.
- **Muốn tạo lại từ đầu:** bỏ dấu `--` ở 4 dòng "TAO LAI TU DAU" đầu file. Cách này xóa hết dữ liệu, nên dừng API trước.

File này **tự sinh, đừng sửa tay**. Sửa `00`–`05` xong thì chạy `.\Database\build-all-in-one.ps1` và commit cả file sinh ra. Muốn kiểm tra file còn khớp với `00`–`05` thì chạy `.\Database\build-all-in-one.ps1 -Check`.

| Thứ tự | File | Nội dung |
| --- | --- | --- |
| 0 | `00_CreateDatabase.sql` | Tạo `NursingHomeDB` (collation `Vietnamese_CI_AS`) |
| 1 | `01_Schema.sql` | 21 bảng lõi (mục 1–8) + bảng module: mục 9 Viên (`Inquiries`, `TourBookings`, `Shifts`, `ShiftAssignments`, `ResidentCareAssignments`, `LeaveRequests`, `ShiftHandovers`, `ExtraServices`, `ServiceUsages`, `InvoiceReminders`, `Conversations`, `Messages`, `AiConversations`, `AiMessages`, `AiKnowledgeDocs`, `FamilyInvitations`, `ResidentDocuments`, `ResidentContracts`, `ResidentLeaves`, `ResidentStays`, `Deposits`, `RoomTypes`, `BedReservations`, `MaintenanceRequests`; cột `Rooms.RoomTypeId` thêm bằng ALTER có kiểm tra ở mục 9.9…), mục 10 Huy (`Incidents`, `MedicalConditions`, `Allergies`, `DietPlans`, `VisitBookings`, `CarePlans`, `CarePlanItems`, `GeneratedCareTasks`, `TaskReminders`, `DailyCareLogs`, `MealLogs`, `VitalThresholds`, `Assessments`, `DoctorVisits`, `Feedbacks`). Khóa, FK, CHECK, unique, filtered index, rowversion |
| 2 | `02_Views_Functions.sql` | `vw_InvoiceBalances`, `vw_BedOccupancy` |
| 3 | `03_StoredProcedures.sql` | `sp_MonthlyRevenue` (báo cáo) |
| 4 | `04_ReferenceData.sql` | Admin đầu tiên + danh mục gói dịch vụ (mọi môi trường) |
| 5 | `05_DemoSeed.sql` | Tài khoản demo mỗi role + kịch bản demo (chỉ local) |
| — | `NursingHomeDB_AllInOne.sql` | `00` → `05` gộp làm một, sinh bằng `build-all-in-one.ps1` |
| — | `upgrades/` | Script nâng cấp một lần cho DB đang có dữ liệu |

Tài khoản demo: xem đầu file `05_DemoSeed.sql`. Mật khẩu chung `Demo@123456`.

## Quy tắc cập nhật (bắt buộc)

1. **Đổi bảng/cột/ràng buộc** → sửa `01_Schema.sql` + Entity trong `BE/NursingHome.BusinessObjects/Entities` + mapping trong `NursingHomeContext.cs`, **cùng một commit**.
2. **Đổi dữ liệu mẫu** → sửa `05_DemoSeed.sql` (danh mục dùng chung thì `04_ReferenceData.sql`). Không tạo file seed thứ hai.
3. **DB của bạn đang có dữ liệu cần giữ** → thêm `upgrades/yyyy-MM-dd_TenThayDoi.sql` có kiểm tra `IF COL_LENGTH(...) IS NULL` / `IF OBJECT_ID(...) IS NULL`, chạy được nhiều lần. File gốc (bước 1) vẫn phải sửa để DB dựng mới luôn đúng.
4. Script phải **idempotent** (chạy lại không lỗi, không nhân bản dữ liệu), có `SET XACT_ABORT ON`, seed dùng `IDENTITY_INSERT` để Id cố định.
5. **Mật khẩu seed**: chỉ dùng hash BCrypt sinh bởi project: `cd BE; dotnet run --project NursingHome.API -- --hash-password "MatKhau@123"`. Không tự bịa hash, không lưu plaintext.
6. Chuỗi tiếng Việt dùng `NVARCHAR` và literal `N'...'`; chạy sqlcmd với `-f 65001`.
7. Sau khi sửa: chạy `setup-local-db.ps1 -Recreate`, rồi `dotnet run --project NursingHome.API -- --check-db`.
8. Chạy `.\Database\build-all-in-one.ps1` để sinh lại `NursingHomeDB_AllInOne.sql`, commit cùng các file `00`–`05` đã sửa.

## Quy ước schema

- Khóa `INT IDENTITY`; bảng log/giao dịch dùng `BIGINT`.
- Enum lưu `NVARCHAR` + `CHECK (... IN (...))`, EF map bằng `HasConversion<string>()`.
- Tiền VND `DECIMAL(18,0)`; số lượng `DECIMAL(9,2)`.
- Thời điểm `DATETIME2` UTC (`SYSUTCDATETIME()`); ngày/giờ lịch `DATE`/`TIME(0)`. Giờ cơ sở UTC+7 quy đổi ở `FacilityClock`.
- FK `NO ACTION`: không xóa dây chuyền, lịch sử được giữ.
- Ràng buộc nghiệp vụ quan trọng nằm ở DB: một giường chỉ một cư dân đang ở (`UX_BedAssignments_*`), một lần dùng thuốc một bản ghi (`UQ_MedicationLogs_Schedule_Time`), một hóa đơn mỗi kỳ (`UQ_Invoices_Resident_Period`), mã thanh toán duy nhất (`UQ_Payments_Reference`, chặn ghi PayOS hai lần).
