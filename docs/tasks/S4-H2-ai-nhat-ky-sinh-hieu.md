# S4-H2 · Nhật ký hằng ngày bằng AI + diễn giải xu hướng sinh hiệu (AI-03, AI-06)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Huy | Should | 5 | 4 | S3-H4, S3-H5, S3-H6, S4-V1 PR 1 | `feature/s4-h2-ai-insights` |

## Mục tiêu

- **AI-03**: mỗi tối hệ thống soạn **bản nháp nhật ký** 4–6 câu cho từng cư dân có người nhà (từ dữ liệu đã chia sẻ trong ngày). Điều dưỡng đọc, sửa, duyệt; chỉ bản đã duyệt mới gửi cho gia đình.
- **AI-06**: hằng ngày tính **xu hướng sinh hiệu** 14 ngày bằng thống kê trong C#; khi bất thường (vượt ngưỡng riêng, tăng/giảm liên tục, lệch xa trung bình) thì AI viết 1–2 câu giải thích cho điều dưỡng/bác sĩ. Không gửi cho gia đình.

## Tiêu chí hoàn thành

- [ ] Job 19:00 giờ VN tạo nháp `DailyDigest` cho cư dân Active có ≥ 1 liên kết gia đình và có dữ liệu trong ngày; chạy lại không trùng (UNIQUE).
- [ ] Màn "Duyệt nhật ký AI" cho Nurse: danh sách nháp hôm nay, sửa nội dung, Duyệt / Bỏ. Duyệt → gia đình nhận chuông "Nhật ký hôm nay của <tên>"; nhật ký hiện đầu tab "Nhật ký hôm nay" của gia đình (S3-H6).
- [ ] Job 06:00 tính xu hướng; phát hiện bất thường → `VitalTrend` + thông báo Nurse/Doctor; hiện ở tab Sinh hiệu của hồ sơ cư dân.
- [ ] Quy tắc phát hiện viết bằng C# và có **unit test**; AI chỉ diễn giải số liệu đã tính, không tự phát hiện.
- [ ] Mọi nội dung AI có nhãn "Do AI soạn" và câu "không thay thế đánh giá của nhân viên y tế".

## 1. Database — `01_Schema.sql` mục 10

```sql
IF OBJECT_ID(N'dbo.AiInsights', N'U') IS NULL
CREATE TABLE dbo.AiInsights (
    Id           BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiInsights PRIMARY KEY,
    ResidentId   INT           NOT NULL CONSTRAINT FK_AiInsights_Residents REFERENCES dbo.Residents (Id),
    Type         NVARCHAR(20)  NOT NULL,
    ForDate      DATE          NOT NULL,
    Content      NVARCHAR(MAX) NOT NULL,
    SourceJson   NVARCHAR(MAX) NULL,      -- số liệu đầu vào (để kiểm tra lại)
    Status       NVARCHAR(10)  NOT NULL CONSTRAINT DF_AiInsights_Status DEFAULT (N'Draft'),
    GeneratedAt  DATETIME2     NOT NULL CONSTRAINT DF_AiInsights_GeneratedAt DEFAULT (SYSUTCDATETIME()),
    ReviewedById INT           NULL CONSTRAINT FK_AiInsights_Reviewer REFERENCES dbo.Accounts (Id),
    ReviewedAt   DATETIME2     NULL,
    CONSTRAINT UQ_AiInsights UNIQUE (ResidentId, Type, ForDate),
    CONSTRAINT CK_AiInsights_Type CHECK (Type IN (N'DailyDigest', N'VitalTrend')),
    CONSTRAINT CK_AiInsights_Status CHECK (Status IN (N'Draft', N'Approved', N'Rejected'))
);
GO
```

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/CareAi.cs` | `AiInsight` |
| DAO | `AiInsightDAO.cs` | `TryAddAsync` (bỏ qua nếu trùng UNIQUE), `ListAsync(date, type, status)`, `ListApprovedForResidentAsync`, `UpdateAsync` |
| Thống kê | `Services/Helpers/VitalTrends.cs` | `Analyze(IReadOnlyList<VitalSign> last14Days, limits) → List<TrendFinding>`: (1) giá trị mới nhất vượt ngưỡng riêng/mặc định; (2) 3 lần đo liên tiếp tăng (hoặc giảm) đều và lần cuối lệch > 10% trung bình; (3) \|z-score\| ≥ 2 so với trung bình 14 ngày (cần ≥ 5 lần đo). **Unit test** từng quy tắc |
| Service | `Services/Ai/DailyDigestService.cs` | Lấy timeline ngày (S3-H6, chỉ mục chia sẻ) → prompt: "Viết 4–6 câu gửi người nhà, giọng ấm áp, chỉ nêu sự kiện có trong dữ liệu, không nhận định y tế" → lưu Draft. `ApproveAsync(id, editedContent, user)` → Approved + thông báo gia đình |
| Service | `Services/Ai/VitalTrendService.cs` | Với cư dân Active: `VitalTrends.Analyze` → có finding thì gọi AI diễn giải 1–2 câu (số liệu nêu rõ) → lưu `VitalTrend` Draft + `NotifyRolesAsync([Nurse, Doctor], Health, ...)` |
| Job | `API/BackgroundJobs/AiInsightJob.cs` | Kiểm mỗi 15 phút: qua 19:00 VN mà chưa chạy hôm nay → digest; qua 06:00 → xu hướng (dùng `AiInsights` làm dấu đã chạy, không cần bảng phụ) |
| Controller | `AiInsightsController.cs` | |

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| GET | `/api/ai-insights?date=&type=&status=` | CarePlanners + Doctor |
| POST | `/api/ai-insights/{id}/approve` `{content}` · `/reject` | CarePlanners |
| POST | `/api/ai-insights/generate?type=DailyDigest&date=` | CarePlanners (chạy tay để demo) |
| GET | `/api/residents/{id}/vital-trends` | ClinicalStaff |
| GET | `/api/family/residents/{id}/digests?date=` | FamilyOnly (chỉ Approved, kiểm link + CanViewCare) |

## 4. Frontend

- `pages/Care/AiDigestReview.tsx` (`/staff/ai-digests`, CarePlanners): thẻ mỗi cư dân, `TextArea` sửa, nút Duyệt/Bỏ, nút "Tạo nháp ngay".
- `VitalsTab` (`ClinicalTabs.tsx`): `Alert` vàng cho xu hướng mới nhất kèm nhãn "Do AI soạn".
- `FamilyResident.tsx` tab "Nhật ký hôm nay": khối nhật ký đã duyệt ở đầu.
- Menu "Duyệt nhật ký AI".

## 5. Kiểm thử

1. "Tạo nháp ngay" cho hôm qua → có nháp NH-0001, NH-0002 (có liên kết gia đình), không có NH-0003 (không liên kết).
2. Sửa + duyệt → `family` nhận chuông, thấy nhật ký; nháp bị Bỏ không hiện.
3. Seed thêm 5 lần đo tâm thu tăng dần cho NH-0001 → chạy xu hướng → có `VitalTrend`, nurse nhận chuông.
4. Unit test `VitalTrends.Analyze` cho 3 quy tắc + trường hợp không đủ dữ liệu.

## Bẫy cần tránh

- Gia đình **không bao giờ** thấy nháp hoặc `VitalTrend`; chỉ `DailyDigest` đã duyệt.
- Không gửi CCCD/SĐT/địa chỉ cho AI; chỉ tên gọi + số liệu trong ngày.
- Job chạy lại sau khi API khởi động lại không được tạo trùng (UNIQUE + `TryAddAsync`).
