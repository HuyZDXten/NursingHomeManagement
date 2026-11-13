# Backlog dự phòng (làm khi xong task chính sớm)

Các use case còn lại của báo cáo, **đã chia sẵn người làm** theo module để không giẫm chân nhau. Chỉ lấy việc ở đây khi task Must/Should của sprint đã merge. Lấy việc nào thì tạo file task từ [_TEMPLATE.md](_TEMPLATE.md) và thêm dòng vào bảng ở [README.md](README.md). Thứ tự trong mỗi bảng là thứ tự nên làm (giá trị demo cao trước).

## Viên — Vận hành, tài khoản, hệ thống

| UC | Việc | Gợi ý làm nhanh | Điểm |
| --- | --- | --- | --- |
| UC-07, UC-24 | Ảnh đại diện tài khoản, ảnh chân dung cư dân | Chép `CloudinaryImageService` + `LocalImageStorageService` từ ASS2 (`FUNews.Services/Implementations`, `FUNews.API/Storage`); endpoint `POST /api/uploads/image` trả URL; dùng `Upload` của antd | 3 |
| UC-02 | Đăng nhập Google | Chép `GoogleTokenValidator` + `GoogleSignInButton.tsx` từ ASS2; chỉ cho tài khoản đã tồn tại | 3 |
| UC-13 | Nhật ký thao tác (audit log) | Bảng `AuditLogs(Id BIGINT, AccountId, Action, Entity, EntityId, DataJson, At)`; ghi bằng `IAuditService.LogAsync` ở các service nhạy cảm (thanh toán, khóa tài khoản, ngừng lưu trú, dừng thuốc); trang xem cho Admin | 3 |
| UC-29, UC-30 | Giấy tờ & hợp đồng lưu trú | `ResidentDocuments`, `ResidentContracts` (file lưu Cloudinary/local), tab trong `Tabs.tsx` | 5 |
| UC-36 | Loại phòng & phụ thu | `RoomTypes(Name, Surcharge)`, `Rooms.RoomTypeId` (sửa schema lõi mục 3 + upgrade script), hóa đơn thêm dòng phụ thu theo ngày | 3 |
| UC-43 | Hồ sơ nhân viên, chứng chỉ, cảnh báo hết hạn | `StaffProfiles`, `StaffCertificates(ExpiresOn)`; job báo Admin trước 30 ngày | 3 |
| UC-129, UC-130 | Cấu hình cơ sở trong DB, danh mục dùng chung | `SystemSettings(Key, Value)` ghi đè `FacilityOptions`; danh mục quan hệ gia đình, loại sự cố | 3 |
| UC-119, UC-120 | Thông báo hàng loạt, chọn kênh nhận | Admin gửi cho mọi Family; `NotificationPreferences(AccountId, Email BIT)` | 2 |
| UC-08 | Mã mời kích hoạt tài khoản Family | `FamilyInvitations(Code, Email, ResidentId, ExpiresAt, UsedAt)`; trang `/kich-hoat?code=` đặt mật khẩu | 3 |
| UC-31, UC-33 | Tạm vắng, tái nhập viện | `ResidentLeaves`; tái nhập: `Status → Active`, tạo phân giường mới (giữ lịch sử) | 3 |
| UC-41, UC-42 | Giữ giường, báo hỏng & sửa chữa | `BedReservations(ExpiresAt)`; `MaintenanceRequests` + trạng thái giường Maintenance | 3 |
| UC-48 | Chấm công vào/ra ca | `Attendances(ShiftAssignmentId, CheckInAt, CheckOutAt)` | 2 |
| UC-99 | Tiền cọc & hoàn cọc | `Deposits(ResidentId, Amount, PaidAt, RefundedAt)`; hoàn khi ngừng lưu trú | 2 |
| UC-126, UC-127 | Hiệu suất nhân viên, dashboard cá nhân | % việc đúng hạn theo người; thẻ "việc hôm nay, ca kế tiếp" ở trang chủ nhân viên | 3 |
| UC-19, UC-115 | Tin tức & album công khai | `Posts(Title, Content, CoverUrl, PublishedAt)`; trang `/tin-tuc` | 3 |
| AI-05 | Trợ lý nhân viên (quy trình nội bộ, việc tiếp theo) | Kênh `Staff` của `AiChatEngine`, tool `my_tasks_today`, `search_knowledge(publicOnly=false)` | 3 |

## Huy — Chăm sóc, y tế, gia đình

| UC | Việc | Gợi ý làm nhanh | Điểm |
| --- | --- | --- | --- |
| UC-79, UC-74 | Danh mục thuốc, bác sĩ duyệt chỉ định trong hệ thống | `Medications(Name, ActiveIngredient, Form)`; lịch thuốc chọn từ danh mục; trạng thái chỉ định `Proposed → Approved` (Nurse đề xuất, Doctor duyệt) | 5 |
| UC-80, UC-81 | Tồn kho thuốc theo cư dân, thuốc dùng khi cần (PRN) | `MedicationStocks(ResidentId, MedicationName, Quantity)`, trừ khi ghi Taken; cảnh báo còn < 3 ngày; PRN = lịch không có giờ cố định, ghi kèm lý do | 5 |
| UC-60 | Ảnh đính kèm ghi chép chăm sóc | Dùng endpoint upload của Viên (UC-07); `CareAttachments(CareRecordId, Url, ShareWithFamily)` | 2 |
| UC-69, UC-70 | Lịch hẹn khám ngoài, tiêm chủng | `Appointments(ResidentId, Place, At, Escort)`, `Vaccinations(Vaccine, Dose, GivenOn, NextDueOn)` + nhắc | 3 |
| UC-72 | Xuất hồ sơ sức khỏe PDF | Dùng QuestPDF như hóa đơn (S3-V5): thông tin, bệnh nền, dị ứng, thuốc, sinh hiệu 30 ngày | 3 |
| UC-112..UC-114, UC-116 | Hoạt động & sự kiện, điểm danh, gia đình đăng ký, thực đơn tuần | `Activities`, `ActivityParticipants`, `MealMenus(WeekStart, Day, Meal, Dishes)`; lịch dạng `Calendar` của antd | 5 |
| UC-105 | Đặt lịch gọi video | Như lịch thăm (S3-H3) với `Type = Video`, nhân viên gửi link Meet/Zalo | 2 |
| UC-111 | Gia đình gửi đồ dùng/thuốc vào | `ItemRequests(ResidentId, Items, Status)`; điều dưỡng xác nhận đã nhận | 2 |
| AI-07 | Điểm nguy cơ tổng hợp (té ngã, sụt cân, suy giảm ADL) | Quy tắc trên `Assessments`, `MealLogs` (% ăn 7 ngày), `Incidents`; AI chỉ diễn giải | 3 |
| AI-08 | Ghi chú bằng giọng nói | Web Speech API (`webkitSpeechRecognition`, `lang='vi-VN'`) điền vào ô kết quả; AI tách thành trường | 3 |
| AI-09 | Đọc ảnh đơn thuốc → nháp lịch thuốc | Gemini vision (ảnh base64) trả JSON; luôn là nháp, Doctor duyệt | 5 |
| AI-10, AI-11 | Cảm xúc phản hồi, gợi ý thực đơn | Nhãn tích cực/tiêu cực + chủ đề cho `Feedbacks`; thực đơn theo `DietPlans`/`Allergies` | 3 |
