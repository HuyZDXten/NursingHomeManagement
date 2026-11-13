# S3-H6 · Nhật ký hằng ngày cho gia đình & phản hồi/khiếu nại (UC-108, UC-109, UC-110)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Huy | Should | 5 | 3.2 (cuối tuần) | S3-H1 (sự cố), S3-H4 (ADL, bữa ăn) | `feature/s3-h6-family-timeline` |

## Mục tiêu

Gia đình mở **"Nhật ký hôm nay"** của người thân: một dòng thời gian gộp các bản ghi đã chia sẻ trong ngày (bữa ăn, ADL, ghi chép chăm sóc, sinh hiệu, sự cố, phiếu khám chia sẻ). Gia đình gửi **phản hồi/khiếu nại** có đánh giá sao; Admin tiếp nhận, giao người xử lý, trả lời — gia đình nhận thông báo. Dòng thời gian này là đầu vào cho AI-03 (S4-H2).

## Tiêu chí hoàn thành

- [ ] `GET` dòng thời gian theo ngày, sắp theo giờ, mỗi mục có loại, giờ, tiêu đề, nội dung, người ghi; **chỉ** gồm bản ghi `ShareWithFamily = 1` và loại mà link cho phép (Care: ghi chép, ADL, bữa ăn, sự cố; Health: sinh hiệu, phiếu khám).
- [ ] Trang gia đình có bộ chọn ngày (tối đa 30 ngày trước), hiển thị `Timeline` có biểu tượng theo loại.
- [ ] Family gửi phản hồi: chủ đề, nhóm (Chăm sóc, Ăn uống, Cơ sở vật chất, Hóa đơn, Nhân viên, Khác), nội dung, số sao 1–5 (tùy chọn), người thân liên quan (tùy chọn).
- [ ] Admin xem danh sách (lọc trạng thái/nhóm), giao cho nhân viên, trả lời; trạng thái New → InProgress → Resolved; gia đình nhận thông báo khi có trả lời.
- [ ] Family chỉ thấy phản hồi của mình; family2 không đọc được của family (404).

## 1. Database — `01_Schema.sql` mục 10

```sql
IF OBJECT_ID(N'dbo.Feedbacks', N'U') IS NULL
CREATE TABLE dbo.Feedbacks (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Feedbacks PRIMARY KEY,
    FamilyAccountId INT            NOT NULL CONSTRAINT FK_Feedbacks_Family REFERENCES dbo.Accounts (Id),
    ResidentId      INT            NULL CONSTRAINT FK_Feedbacks_Residents REFERENCES dbo.Residents (Id),
    Category        NVARCHAR(20)   NOT NULL,
    Rating          TINYINT        NULL,
    Subject         NVARCHAR(200)  NOT NULL,
    Content         NVARCHAR(2000) NOT NULL,
    Status          NVARCHAR(20)   NOT NULL CONSTRAINT DF_Feedbacks_Status DEFAULT (N'New'),
    AssignedToId    INT            NULL CONSTRAINT FK_Feedbacks_Assignee REFERENCES dbo.Accounts (Id),
    Response        NVARCHAR(2000) NULL,
    RespondedById   INT            NULL CONSTRAINT FK_Feedbacks_Responder REFERENCES dbo.Accounts (Id),
    RespondedAt     DATETIME2      NULL,
    CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_Feedbacks_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Feedbacks_Category CHECK (Category IN (N'Care', N'Food', N'Facility', N'Billing', N'Staff', N'Other')),
    CONSTRAINT CK_Feedbacks_Rating CHECK (Rating IS NULL OR Rating BETWEEN 1 AND 5),
    CONSTRAINT CK_Feedbacks_Status CHECK (Status IN (N'New', N'InProgress', N'Resolved'))
);
GO
```

Dòng thời gian **không cần bảng mới** — đọc từ `CareRecords`, `DailyCareLogs`, `MealLogs`, `VitalSigns`, `Incidents`, `DoctorVisits`.

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/CareFamily.cs` | `Feedback` + enum |
| DAO | `FamilyTimelineDAO.cs` | `ForDateAsync(residentId, startUtc, endUtc, includeCare, includeHealth)` — 6 truy vấn `AsNoTracking`, map về record chung `TimelineEntry(Kind, At, Title, Detail, By)` |
| DAO | `FeedbackDAO.cs` | CRUD, `ListAsync(familyId?, status?, category?)` |
| DTOs | `DTOs/Care/FamilyTimelineDtos.cs` | `TimelineItemDto {kind, at, title, detail, recordedBy}`, `FeedbackDto`, `CreateFeedbackRequest`, `RespondFeedbackRequest {status, response, assignedToId}` |
| Service | `FamilyTimelineService.cs`, `FeedbackService.cs` | Quy tắc bên dưới |
| Controller | `FamilyTimelineController.cs`, `FeedbacksController.cs` | |

Quy tắc:
- Timeline: kiểm link (`residents.GetLinkAsync(residentId, user.AccountId)`), ngày trong [hôm nay - 30, hôm nay]; khoảng thời gian = `clock.StartOfDayUtc(date)` đến +1 ngày; `includeCare = link.CanViewCare`, `includeHealth = link.CanViewHealth`.
- Mô tả mỗi loại: bữa ăn "Bữa trưa: ăn 75%, uống 300 ml"; ADL "Tắm: cần hỗ trợ"; sinh hiệu "HA 130/80, mạch 76, 36.6°C"; sự cố "<loại> — <kết luận>" (chỉ sự cố đã đóng và chia sẻ).
- Phản hồi: `ResidentId` (nếu có) phải thuộc người thân của mình. Tạo mới → báo Admin. Trả lời → `NotifyAsync([familyId], System, "Cơ sở đã phản hồi: <chủ đề>", ..., "/family/feedback")`.

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| GET | `/api/family/residents/{id}/timeline?date=` | FamilyOnly |
| GET | `/api/feedbacks/mine` | FamilyOnly |
| POST | `/api/feedbacks` | FamilyOnly |
| GET | `/api/feedbacks?status=&category=` | AdminOnly |
| POST | `/api/feedbacks/{id}/respond` | AdminOnly |

## 4. Frontend

- `pages/Family/FamilyResident.tsx` (file của Huy): tab đầu tiên **"Nhật ký hôm nay"** — `DatePicker` + `Timeline` (icon theo `kind`: bữa ăn `CoffeeOutlined`, ADL `SmileOutlined`, sinh hiệu `HeartOutlined`, sự cố `WarningOutlined`, khám `MedicineBoxOutlined`).
- `pages/Family/FamilyFeedback.tsx` (`/family/feedback`): form gửi + danh sách phản hồi của tôi kèm câu trả lời.
- `pages/Care/Feedbacks.tsx` (`/staff/feedback`, AdminOnly): bảng + `Drawer` trả lời.
- Menu "Góp ý" (Family) và "Phản hồi gia đình" (Admin) trong `modules/care.tsx`.

## 5. Dữ liệu demo — mục 11

2 phản hồi: `family` (Food, 4 sao, Resolved, có trả lời), `family2` (Facility, New). Dòng thời gian dùng dữ liệu đã seed ở S3-H1/H4 (nhớ `ShareWithFamily = 1`).

## 6. Kiểm thử

1. `family` mở nhật ký hôm qua của NH-0001 → thấy bữa ăn, ADL, sinh hiệu đã chia sẻ; không thấy bản ghi `ShareWithFamily = 0`.
2. Tắt quyền Health của `family` (lễ tân) → nhật ký không còn sinh hiệu.
3. `family2` gửi phản hồi → `admin` thấy chuông, trả lời → `family2` thấy chuông + câu trả lời.
4. `family` gọi timeline của NH-0002 → 404.

## Tham khảo code có sẵn

`FamilyService.DetailsAsync` (lọc theo cờ quyền + ShareWithFamily), `pages/Family/FamilyResident.tsx`, `components/CareTaskModals.tsx` → `TaskRecordsDrawer` (Timeline).

## Bẫy cần tránh

- Mỗi nguồn dữ liệu tự kiểm `ShareWithFamily`; quên một nguồn là lộ dữ liệu y tế.
- Lọc theo ngày VN (`StartOfDayUtc`), không theo ngày UTC.
