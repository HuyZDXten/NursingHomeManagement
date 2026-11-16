# Hướng dẫn cho Huy — bắt đầu Sprint 3

Cập nhật 16/11/2026 · Viên viết cho Huy

Viên đã code xong 10 task chính (S3-V1 → S4-V2) và **đã merge cả vào `develop` ngày 16/11** (smoke test 213/213, unit test 87/87). Huy có 9 task, 48 điểm. Việc đầu tiên là **S3-H1 Sự cố**, vì báo cáo UC-125 và trợ lý AI của Viên đang chờ bảng `Incidents`.

Tài liệu này chỉ ghi những gì đã đổi kể từ lúc chia task và cách bắt đầu. Chi tiết từng task vẫn nằm trong file task của Huy ([README](README.md#bảng-task)).

## 1. Dựng máy trên code mới nhất

`develop` là code mới nhất, đủ Sprint 0–4 của Viên.

```powershell
git fetch origin --prune             # --prune xóa tên nhánh cũ đã đổi (mục 2)
git checkout develop; git pull
.\Database\setup-local-db.ps1        # NursingHomeDB đủ bảng Sprint 3–4 + dữ liệu demo
Copy-Item BE\NursingHome.API\appsettings.Development.example.json BE\NursingHome.API\appsettings.Development.json
#   đổi Jwt:Key (>= 32 ký tự). File mẫu đã có "Ai": { "Enabled": true, "Provider": "Fake" }
dotnet build BE/NursingHome.sln      # 0 lỗi, 0 cảnh báo
dotnet test BE/NursingHome.sln       # 87/87
.\scripts\run-smoke-isolated.ps1     # 213/213, chạy trên DB riêng NursingHomeDB_Test
```

- Đã có `appsettings.Development.json` từ trước: chỉ cần thêm khối `"Ai"` như trên, không có thì chatbot và trợ lý AI tắt.
- Đã có DB cũ: chạy `setup-local-db.ps1` không kèm `-Recreate` cũng được, bảng mới được tạo và dữ liệu cũ giữ nguyên. Muốn dữ liệu demo mới thì thêm `-Recreate`.
- AI chạy bằng provider giả (Fake), không cần khóa Gemini. Khóa thật chỉ để trong user-secrets, không ghi vào file.

Xem nhanh phần Viên đã làm: đăng nhập `admin@nursinghome.local` (mật khẩu `Demo@123456`), mở Ca trực, Báo cáo, Tin nhắn. Sau đó mở trang chủ công khai và thử chatbot ở góc phải.

## 2. Nhánh và PR

Mọi nhánh mới tạo từ `develop`, đặt tên theo kiểu StreetBiz: `<loại>/<mô-tả-chức-năng>` bằng tiếng Anh, không ghi mã task ([HANDOVER mục 9](../HANDOVER.md#9-quy-trình-git)). Tên nhánh của từng task đã ghi sẵn trong file task, ví dụ S3-H1 là `feature/incidents`.

```powershell
git checkout develop; git pull
git checkout -b feature/incidents
# ... code, build, test ...
git push -u origin feature/incidents   # mở PR vào develop, Viên review
```

Merge PR bằng **Create a merge commit**, không dùng Squash hay Rebase, để lịch sử từng nhánh còn nguyên.

### Xem lại 10 nhánh của Viên

10 nhánh dưới đây đã merge thẳng vào `develop`, **không qua PR**, nên Huy chưa review. Mỗi nhánh tạo từ nhánh ngay trên nó. Xem phần thay đổi riêng của nhánh bằng `git diff <nhánh trên>..<nhánh>`, hoặc trên GitHub bằng trang Compare. Thấy lỗi thì mở nhánh `fix/<mô-tả>` từ `develop` hoặc nhắn Viên.

| # | Nhánh (tên mới) | Tên cũ | Cần xem kỹ |
| --- | --- | --- | --- |
| 1 | `feature/admissions-tours` | `feature/s3-v1-admissions` | Form công khai (tư vấn, đặt tham quan) có rate limit |
| 2 | `feature/staff-shifts` | `feature/s3-v2-shifts` | Ca trực, phân công người phụ trách chính |
| 3 | `feature/caregiver-access` | `feature/s3-v2-access` | **`ResidentAccessService` (file lõi, ảnh hưởng mọi màn chăm sóc).** Caregiver giờ xem được cả cư dân mình phụ trách chính, không chỉ cư dân đang có việc |
| 4 | `feature/reports` | `feature/s3-v3-reports` | Báo cáo + Excel; UC-125 chưa có vì chờ S3-H1 |
| 5 | `feature/leave-shift-handover` | `feature/s3-v4-handover` | Gợi ý bàn giao ca gọi `HealthService.OutOfRange` của Huy |
| 6 | `feature/billing-extras` | `feature/s3-v5-billing-extras` | Sửa lõi: `BillingDAO.CreateInvoiceAsync` chạy trong transaction; `[Range]` decimal đọc theo invariant culture |
| 7 | `feature/family-chat` | `feature/s3-v6-chat` | Chat gia đình – nhân viên qua SignalR (sự kiện `chat`) |
| 8 | `feature/ai-core` | `feature/s4-v1-ai-core` | **Hạ tầng AI Huy dùng cho S4-H1** |
| 9 | `feature/public-chatbot` | `feature/s4-v1-public-bot` | Chatbot công khai, chỉ ghi dữ liệu khi khách trả lời "đồng ý" |
| 10 | `feature/ai-handover-reports` | `feature/s4-v2-ai-handover` | AI tóm tắt bàn giao ca, hỏi đáp số liệu báo cáo, file hướng dẫn này |

## 3. Task của Huy: thứ tự và điều mới cần biết

| Task | Tuần | Phụ thuộc | Điều mới so với file task |
| --- | --- | --- | --- |
| [S3-H1](S3-H1-su-co.md) Sự cố | 16–20/11, làm đầu tiên | — | Giữ đúng tên cột `Incidents` như file task. Merge xong nhắn Viên |
| [S3-H2](S3-H2-benh-nen-di-ung-che-do-an.md) Bệnh nền, dị ứng | 16–20/11 | — | — |
| [S3-H3](S3-H3-lich-tham.md) Lịch thăm | 16–20/11 | — | Báo duyệt/từ chối bằng `INotificationService.NotifyAsync`; chuông tự cập nhật realtime |
| [S3-H4](S3-H4-ke-hoach-cham-soc-adl-bua-an.md) Kế hoạch chăm sóc, ADL, bữa ăn | 23–29/11 | S3-V2 (đã code) | Người phụ trách chính lấy từ `IStaffingRepository.ListCareAssignmentsAsync(residentId, null, activeOnly: true)`; khung giờ ca ở `StaffingRules.ShiftWindow` |
| [S3-H5](S3-H5-danh-gia-phieu-kham-nguong.md) Đánh giá, ngưỡng riêng | 23–29/11 | — | `OutOfRange(vital, limits)` đã nhận `VitalAlertOptions`. Chỉ cần đổi ngưỡng riêng sang `VitalAlertOptions` rồi truyền vào, **không đổi chữ ký**: `HandoverService` của Viên cũng gọi hàm này |
| [S3-H6](S3-H6-nhat-ky-phan-hoi-gia-dinh.md) Nhật ký & phản hồi | 26–29/11 | S3-H1, S3-H4 | Có thể thêm nút "Nhắn tin cho cơ sở" trên thẻ người thân ở `FamilyHome`, trỏ tới `/family/messages` |
| [S4-H1](S4-H1-ai-tro-ly-gia-dinh.md) Trợ lý gia đình | 30/11–06/12 | Hạ tầng AI (đã có trên `develop`) | Xem mục 4 bên dưới |
| [S4-H2](S4-H2-ai-nhat-ky-sinh-hieu.md) Nhật ký AI, sinh hiệu | 30/11–06/12 | S3-H4, S3-H5, S3-H6 | Mẫu gần nhất là `Services/Ai/HandoverAiService.cs`: gọi provider không kèm tool, nhiệt độ thấp; provider Fake hoặc lỗi thì trả bản tóm tắt theo quy tắc |
| [S5-H1](S5-H1-hoan-thien.md) Hoàn thiện | 07–13/12 | tất cả | — |

## 4. Dùng hạ tầng AI cho S4-H1

Cách viết tool và service đã ghi trong [S4-V1, đoạn "Huy dùng hạ tầng cho S4-H1"](S4-V1-ai-ha-tang-chatbot.md). Ba điểm dễ sót:

- **Rate limit:** endpoint `/api/ai/family/chat` dùng `[EnableRateLimiting(RateLimitPolicies.Chat)]`, đếm 20 tin/phút theo tài khoản.
- **Provider Fake chọn tool theo từ khóa** trong mảng `KeywordTools` của `Services/Ai/FakeAiChatProvider.cs`, và chỉ chọn tool có trong bộ tool của request.
    - Câu có chữ "thuốc", "huyết áp", "bệnh"… mà không khớp từ khóa nào sẽ bị từ chối như câu hỏi y tế.
    - Thêm từ khóa cho tool của Huy vào mảng đó (file của Viên, báo trước). Trong smoke test có thể gọi thẳng tool: `/tool get_recent_vitals {"residentId":1}`.
- **Widget:** gắn `FamilyAssistant` vào `AppLayout` là sửa file lõi. Huy nhắn Viên thêm dòng đó khi trang đã sẵn sàng.

## 5. Mã của Viên mà Huy sẽ dùng

| Cần | Dùng |
| --- | --- |
| Kiểm quyền xem cư dân | `IResidentAccessService.CanReadAsync(user, residentId, section)`, `VisibleResidentIdsAsync(user)` (trả `null` = thấy hết) |
| Người phụ trách chính | `IStaffingRepository.IsPrimaryCaregiverAsync`, `PrimaryResidentIdsAsync`, `ListCareAssignmentsAsync` |
| Ca trực, giờ ca | `StaffingRules.ShiftWindow`, `NextShift`, `WeekStart` |
| Chuông thông báo + realtime | `INotificationService.NotifyAsync`, `NotifyRolesAsync` |
| Email | `IEmailService`: kiểm `IsConfigured` trước khi gửi, chưa cấu hình SMTP thì bỏ qua như `InvoiceMailer` |
| Excel, PDF | `Helpers/ExcelExporter.cs`; QuestPDF như `Helpers/InvoicePdf.cs` |
| Ngày giờ của cơ sở | `FacilityClock` (UTC+7) |
| AI | `IAiTool`, `IAiChatEngine`, `AiToolJson`, `AiChannels.Family`; FE `components/ChatWidget.tsx` |

## 6. Lỗi đã gặp, đừng lặp lại

- `[Range(typeof(decimal), "0.01", …)]` trên máy cài tiếng Việt đọc sai giới hạn và trả 500. Thêm `ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true`.
- OData chỉ dùng attribute routing. Entity set mới cần `EntitySet` trong `EdmModelBuilder.Care.cs` **và** một `ODataController` có `[Route("odata/TenEntitySet")]`, thiếu thì trả 404.
- Ghi nhiều bảng trong một thao tác: làm trong **một** method DAO với transaction Serializable. Lỗi trùng khóa, khóa ngoại, deadlock tự thành 409.
- `DATETIME2` lưu UTC. "Hôm nay" của cơ sở lấy từ `FacilityClock`, không dùng `DateTime.Now`.
- Enum lưu dạng NVARCHAR: `HasConversion<string>()` + CHECK trong schema, cùng tên giá trị.
- Kiểm thử thêm vào cuối `scripts/smoke-test.mjs` trong một khối riêng có tiêu đề task, để ít xung đột với Viên.
- Không commit `appsettings.Development.json`, `FE/.env` hay khóa thật.

## 7. Ai chờ ai

| Người chờ | Việc | Mở khóa khi |
| --- | --- | --- |
| Viên | UC-125, tool AI `incident_summary`, mục sự cố trong gợi ý bàn giao ca | S3-H1 merge |
| Viên | Dùng ngưỡng sinh hiệu riêng trong gợi ý bàn giao ca | S3-H5 merge |
| Huy | Dòng gắn trợ lý gia đình vào `AppLayout` | Huy nhắn Viên |
| Cả hai | Task Must vào `develop` | 20/11 |

## 8. Trước khi mở mỗi PR

- [ ] `dotnet build BE/NursingHome.sln` 0 cảnh báo, `dotnet test` xanh
- [ ] `.\scripts\run-smoke-isolated.ps1` "TẤT CẢ PASS", đã thêm kiểm tra của task
- [ ] FE `npm run build` và `npm run lint` sạch
- [ ] Thử tay một role được phép và một role bị chặn (403/404, không lộ dữ liệu)
- [ ] File task có mục "Kết quả triển khai"; cột Trạng thái trong [README](README.md) đổi sang "Chờ review"
- [ ] API mới vào [ARCHITECTURE](../ARCHITECTURE.md) mục 7, bảng mới vào [Database/README](../../Database/README.md)
