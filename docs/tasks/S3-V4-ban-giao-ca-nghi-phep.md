# S3-V4 · Nghỉ phép/đổi ca & bàn giao ca (UC-47, UC-49)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Should | 5 | 3.2 | S3-V2 (bảng ca) | `feature/s3-v4-handover` |

## Mục tiêu

Nhân viên xin nghỉ hoặc xin đổi ca với đồng nghiệp, Admin duyệt (đổi ca thì hệ thống tự hoán đổi lịch). Cuối ca, điều dưỡng/hộ lý viết **bàn giao ca**: hệ thống gợi ý sẵn việc chưa xong, thuốc bị từ chối/bỏ lỡ, sinh hiệu vượt ngưỡng, sự cố trong ca; ca sau xác nhận đã đọc. Bàn giao là đầu vào cho AI-04 (S4-V2).

## Tiêu chí hoàn thành

- [x] Nhân viên tạo đơn **Nghỉ** (từ ngày–đến ngày, lý do) hoặc **Đổi ca** (chọn một ca của mình + đồng nghiệp + ca của đồng nghiệp).
- [x] Admin duyệt/từ chối có ghi chú. Duyệt đổi ca → hoán đổi `AccountId` của 2 `ShiftAssignments` trong **một transaction**. Duyệt nghỉ → xóa các ca của người đó trong khoảng ngày và báo Admin "cần xếp người thay" (kèm danh sách ca bị trống).
- [x] Người xin và đồng nghiệp (khi đổi ca) nhận thông báo kết quả.
- [x] "Bàn giao ca": chọn ngày + ca → nút **Lấy gợi ý** điền sẵn danh sách; người viết sửa/thêm nội dung; lưu.
- [x] Ca kế tiếp thấy bàn giao chưa đọc (badge), bấm "Đã đọc" thì lưu người + giờ.
- [x] Caregiver chỉ thấy đơn của mình; Family/Receptionist → 403.

### Kết quả triển khai (13/11/2026)

- Kiểm thử: smoke thêm 34 kiểm tra (tổng 128/128 PASS: đổi ca hoán đúng người, duyệt 2 lần bị chặn, duyệt nghỉ trả danh sách ca trống, gợi ý bàn giao lọc theo quyền, xác nhận đã đọc); unit thêm `HandoverRuleTests` (52/52).
- Hai tab mới trong `/staff/shifts` build/lint sạch nhưng **chưa xem trên trình duyệt**. Tab đang mở nằm trên URL (`?tab=leave`, `?tab=handover`) để thông báo trỏ thẳng vào.
- Quyết định khác/bổ sung so với đặc tả:
  - Thêm `GET /api/leave-requests/swap-candidates?shiftAssignmentId=`: Caregiver/Doctor không xem được lịch của người khác, nên server trả sẵn các ca đổi được (cùng role, 4 tuần tới, chưa bắt đầu, không làm ai bị trùng ca).
  - Danh sách đơn của người không phải Admin gồm cả đơn **đồng nghiệp xin đổi ca với mình** (để biết trước).
  - Khi một ca bị bỏ (Admin bỏ ca, duyệt nghỉ) hoặc đã đổi theo đơn khác, các đơn đổi ca đang chờ có dính ca đó **tự hủy** và báo người liên quan; đơn cũ được gỡ tham chiếu tới ca đã xóa. Vì vậy `CK_LeaveRequests_Swap` chỉ bắt buộc đủ 2 ca khi đơn còn `Pending`.
  - Duyệt nghỉ chỉ bỏ ca **chưa bắt đầu**; mỗi đơn nghỉ tối đa 31 ngày, không trùng đơn nghỉ đang chờ.
  - Admin không có ca nên không tạo đơn, nhưng được viết bàn giao không cần ca (viết thay). Không tự xác nhận bàn giao của mình.
  - "Ca kế tiếp" = ca bắt đầu đúng lúc ca này kết thúc (`StaffingRules.NextShift`: sáng → chiều → đêm → sáng hôm sau). Người được xác nhận: có ca kế tiếp, hoặc Admin/Nurse. Caregiver chỉ thấy bàn giao của mình hoặc bàn giao mình cần nhận.
  - Thêm `GET /api/handovers/inbox` (chưa đọc 2 ngày gần nhất mà mình xác nhận được) cho badge ở tab "Bàn giao ca".
  - Gợi ý bàn giao lọc theo cư dân người viết được xem (`IResidentAccessService.VisibleResidentIdsAsync`); mục **sự cố** chờ bảng `Incidents` của S3-H1 (đánh dấu sẵn trong `HandoverService.GetDraftAsync`).
  - `HighlightsJson` lưu mảng `{kind, residentId, residentName, text}` (camelCase) — S4-V2 dùng làm đầu vào AI.

## 1. Database — `01_Schema.sql` mục 9

```sql
IF OBJECT_ID(N'dbo.LeaveRequests', N'U') IS NULL
CREATE TABLE dbo.LeaveRequests (
    Id                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveRequests PRIMARY KEY,
    AccountId             INT           NOT NULL CONSTRAINT FK_LeaveRequests_Accounts REFERENCES dbo.Accounts (Id),
    Type                  NVARCHAR(10)  NOT NULL,
    FromDate              DATE          NOT NULL,
    ToDate                DATE          NOT NULL,
    ShiftAssignmentId     INT           NULL CONSTRAINT FK_LeaveRequests_MyShift REFERENCES dbo.ShiftAssignments (Id),
    SwapWithAccountId     INT           NULL CONSTRAINT FK_LeaveRequests_SwapWith REFERENCES dbo.Accounts (Id),
    SwapShiftAssignmentId INT           NULL CONSTRAINT FK_LeaveRequests_TheirShift REFERENCES dbo.ShiftAssignments (Id),
    Reason                NVARCHAR(500) NOT NULL,
    Status                NVARCHAR(20)  NOT NULL CONSTRAINT DF_LeaveRequests_Status DEFAULT (N'Pending'),
    ReviewedById          INT           NULL CONSTRAINT FK_LeaveRequests_Reviewer REFERENCES dbo.Accounts (Id),
    ReviewedAt            DATETIME2     NULL,
    ReviewNote            NVARCHAR(500) NULL,
    CreatedAt             DATETIME2     NOT NULL CONSTRAINT DF_LeaveRequests_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_LeaveRequests_Type CHECK (Type IN (N'Leave', N'Swap')),
    CONSTRAINT CK_LeaveRequests_Status CHECK (Status IN (N'Pending', N'Approved', N'Rejected', N'Cancelled')),
    CONSTRAINT CK_LeaveRequests_Dates CHECK (ToDate >= FromDate),
    CONSTRAINT CK_LeaveRequests_Swap CHECK (Type = N'Leave' OR (ShiftAssignmentId IS NOT NULL AND SwapShiftAssignmentId IS NOT NULL))
);
GO
IF OBJECT_ID(N'dbo.ShiftHandovers', N'U') IS NULL
CREATE TABLE dbo.ShiftHandovers (
    Id               BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShiftHandovers PRIMARY KEY,
    ShiftId          INT            NOT NULL CONSTRAINT FK_ShiftHandovers_Shifts REFERENCES dbo.Shifts (Id),
    WorkDate         DATE           NOT NULL,
    FromAccountId    INT            NOT NULL CONSTRAINT FK_ShiftHandovers_From REFERENCES dbo.Accounts (Id),
    Summary          NVARCHAR(4000) NOT NULL,
    HighlightsJson   NVARCHAR(MAX)  NULL,   -- các mục gợi ý đã chọn (JSON), S4-V2 dùng lại
    AiGenerated      BIT            NOT NULL CONSTRAINT DF_ShiftHandovers_Ai DEFAULT (0),
    AcknowledgedById INT            NULL CONSTRAINT FK_ShiftHandovers_Ack REFERENCES dbo.Accounts (Id),
    AcknowledgedAt   DATETIME2      NULL,
    CreatedAt        DATETIME2      NOT NULL CONSTRAINT DF_ShiftHandovers_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_ShiftHandovers UNIQUE (WorkDate, ShiftId, FromAccountId)
);
GO
```

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/OperationsStaffing.cs` (thêm vào) | `LeaveRequest`, `ShiftHandover` |
| DAO | `StaffingDAO.cs` (thêm vào) | `ApproveSwapAsync(requestId, reviewerId, now)` — transaction Serializable: đọc 2 assignment, kiểm vẫn thuộc đúng 2 người và đơn còn Pending, hoán `AccountId`, đổi trạng thái đơn. `ApproveLeaveAsync` — xóa assignment trong khoảng, trả danh sách ca bị trống. `GetHandoverDraftDataAsync(windowStartUtc, windowEndUtc)` gom dữ liệu gợi ý |
| DTOs | `DTOs/Staffing/HandoverDtos.cs` | `CreateLeaveRequest`, `LeaveRequestDto`, `ReviewRequest {note}`, `HandoverDraftDto {items: [{kind, residentId, residentName, text}]}`, `CreateHandoverRequest {shiftId, workDate, summary, highlights[]}`, `HandoverDto` |
| Service | `HandoverService.cs` | Quy tắc bên dưới |
| Controller | `HandoversController.cs`, `LeaveRequestsController.cs` | |

Gợi ý bàn giao (`GetDraftAsync(shiftId, workDate)`) trong khung giờ của ca (dùng hàm `ShiftWindow` từ S3-V2):

- `CareTasks` hạn trong ca, trạng thái Pending/InProgress → "Chưa xong: <việc> — <cư dân>".
- `MedicationLogs` Missed/Refused trong ca → "Thuốc <tên> bị <từ chối/bỏ lỡ>: <ghi chú>".
- `VitalSigns` trong ca vượt ngưỡng (dùng lại `HealthService.OutOfRange`) → "Sinh hiệu cần theo dõi".
- `Incidents` trong ca (khi S3-H1 đã merge) → "Sự cố <loại> (<mức độ>)".

Quy tắc:

- Chỉ tạo đơn cho ca của chính mình và ca chưa diễn ra; đồng nghiệp đổi ca phải cùng role.
- Viết bàn giao: người viết phải có ca đó trong `ShiftAssignments` (Admin được viết thay). Mỗi người một bàn giao mỗi ca (UNIQUE).
- "Đã đọc": người có ca **kế tiếp**, hoặc Admin/Nurse.

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| POST | `/api/leave-requests` | ClinicalStaff |
| GET | `/api/leave-requests?status=` | ClinicalStaff (Admin thấy tất cả, người khác chỉ đơn của mình) |
| POST | `/api/leave-requests/{id}/approve` · `/reject` | AdminOnly |
| POST | `/api/leave-requests/{id}/cancel` | ClinicalStaff (chủ đơn, khi còn Pending) |
| GET | `/api/handovers/draft?shiftId=&date=` | ClinicalStaff |
| GET | `/api/handovers?date=` | ClinicalStaff |
| POST | `/api/handovers` | ClinicalStaff |
| POST | `/api/handovers/{id}/acknowledge` | ClinicalStaff |

## 4. Frontend

Thêm 2 tab vào `pages/Operations/Shifts.tsx` (của S3-V2):

- **Nghỉ phép / đổi ca**: bảng đơn (của tôi; Admin thấy tất cả, lọc Pending), modal tạo đơn (`Segmented` Nghỉ/Đổi ca; đổi ca thì chọn ca của mình → chọn đồng nghiệp → chọn ca của đồng nghiệp). Admin có nút Duyệt/Từ chối.
- **Bàn giao ca**: chọn ngày + ca → "Lấy gợi ý" hiện `Checkbox.Group` các mục → `TextArea` tóm tắt (tự điền từ các mục đã chọn) → Lưu. Danh sách bàn giao trong ngày có badge "Chưa đọc" và nút "Đã đọc".

## 5. Dữ liệu demo

1 đơn nghỉ Pending của caregiver2; 1 bàn giao ca sáng hôm qua của nurse (đã đọc) và 1 của hôm nay (chưa đọc).

## 6. Kiểm thử

1. `caregiver` xin đổi ca sáng thứ Ba với `caregiver2` ca chiều → `admin` duyệt → lịch tuần hoán đổi đúng, cả hai nhận thông báo.
2. Duyệt cùng một đơn ở 2 tab trình duyệt → lần 2 bị chặn (đơn không còn Pending).
3. `nurse` lấy gợi ý ca sáng hôm nay → thấy việc "Đo sinh hiệu buổi sáng" chưa xong (dữ liệu seed).
4. Smoke test: check duyệt đổi ca hoán đổi đúng người.

## Tham khảo code có sẵn

`ResidentDAO.DischargeAsync` (nhiều bảng trong một transaction), `HealthService.OutOfRange` (ngưỡng), `MedicationService.TodayAsync` (lọc theo khung giờ).

## Bẫy cần tránh

- Kiểm "đơn còn Pending" và hoán đổi phải nằm trong cùng transaction, tránh duyệt 2 lần.
- Ca đêm qua nửa đêm: dùng chung hàm `ShiftWindow` của S3-V2, không tự tính lại.
