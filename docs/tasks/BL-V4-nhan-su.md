# BL-V4 · Nhân sự: hồ sơ & chứng chỉ, chấm công, hiệu suất, trang chủ cá nhân, trợ lý AI (UC-43, UC-48, UC-126, UC-127, AI-05)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Could (backlog) | 11 | 17/11 | S3-V2 (ca trực), S3-V4 (bàn giao), S4-V1 (hạ tầng AI) | `feature/staff-hr` |

## Mục tiêu

Admin quản lý hồ sơ và chứng chỉ nhân viên (có cảnh báo hết hạn), nhân viên chấm công vào/ra ca, Admin xem hiệu suất; điều dưỡng/hộ lý có trang chủ cá nhân kèm trợ lý AI trả lời về việc của chính họ.

## Kết quả triển khai (17/11/2026)

- [x] **UC-43 hồ sơ & chứng chỉ** (bảng `StaffProfiles`, `StaffCertificates`, schema 9.10): Admin sửa hồ sơ (chức danh, bộ phận, ngày vào làm, CCCD, liên hệ khẩn cấp) và chứng chỉ; nhân viên chỉ xem hồ sơ của chính mình (CCCD là dữ liệu nhạy cảm; xem người khác → 403).
    - Job nền `CertificateExpiryJob` (mặc định 6 giờ, `Staff:CertificateJobEnabled`) báo nhân viên và Admin khi chứng chỉ còn ≤ 30 ngày và khi đã hết hạn; **mỗi mốc một lần** (cột `Alert30At`, `AlertExpiredAt` cập nhật có điều kiện trước khi gửi, nhiều instance cũng không báo trùng). Đổi ngày hết hạn thì mốc được báo lại. Admin có nút "Gửi cảnh báo ngay".
- [x] **UC-48 chấm công** (bảng `Attendances`, mỗi phân ca một dòng, `UQ_Attendances_Assignment`): vào ca từ 30 phút trước giờ bắt đầu tới hết ca; vào trễ quá 15 phút tính đi muộn (số phút tính từ giờ bắt đầu ca). Vào lần hai → 409, ra ca khi chưa vào → 400, ca của người khác → 403.
    - Chỉ Admin sửa/bổ sung công (ví dụ ca vắng do quên chấm công), **bắt buộc lý do**, lưu người sửa + giờ sửa; chỉ sửa được công trong 31 ngày gần nhất, giờ không ở tương lai và không lệch quá 12 giờ so với ca.
- [x] **UC-126 hiệu suất** (Admin, tab "Hiệu suất nhân viên" ở trang Báo cáo + Excel): % việc chăm sóc xong đúng hạn (hạn + 15 phút dung sai, chỉ tính việc đã tới hạn, không tính việc đã hủy), việc quá hạn chưa xong, số lần ghi thuốc, chuyên cần (ca đã kết thúc: có công / đi muộn / vắng). **Chỉ đọc** bảng `CareTasks`, `CareRecords`, `MedicationLogs` của module Chăm sóc.
- [x] **UC-127 trang chủ cá nhân** `/staff/home` (menu cho Điều dưỡng, Hộ lý, Bác sĩ): nút Vào ca/Ra ca, ca kế tiếp, việc hôm nay (cả việc quá hạn từ trước), bàn giao và tin nhắn chưa đọc, chứng chỉ của mình sắp hết hạn. `GET /api/staff-home` mở cho mọi nhân viên; Gia đình → 403.
- [x] **AI-05 trợ lý nhân viên** (kênh `Assistant` mới, tách khỏi kênh `Staff` của hỏi đáp báo cáo): tool `my_tasks_today`, `my_next_shift`, `handover_inbox` (đều dùng `CurrentUser` của người hỏi) và `search_knowledge(publicOnly = false)`. Chỉ đọc; câu hỏi thuốc/chẩn đoán bị từ chối. Tài liệu nội bộ mới (id 7, `IsPublic = 0`) về chấm công, đổi ca, bàn giao: chatbot công khai không dùng. Rate limit `Chat` (20 tin/phút/tài khoản).
    - Ràng buộc `CK_AiConversations_Channel` thêm giá trị `Assistant`: schema 9.10 tự sửa ràng buộc cho DB tạo trước đó (chạy lại `setup-local-db.ps1` không `-Recreate`).
- Seed 10.10: hồ sơ nhân viên 2–7; chứng chỉ bác sĩ còn 2 năm, điều dưỡng còn 20 ngày, hộ lý đã hết hạn 5 ngày; chấm công **tuần trước** (điều dưỡng đủ 5 ca đúng giờ; hộ lý trễ 25 phút thứ Ba và thứ Năm, vắng thứ Sáu).
- Sửa lõi (commit riêng): `homePath` — Điều dưỡng và Hộ lý vào thẳng `/staff/home` sau khi đăng nhập (trước là `/staff/care-tasks`).
- Smoke cô lập tắt job nền chứng chỉ (`Staff__CertificateJobEnabled=false`) để số lượt cảnh báo xác định.
- Kiểm thử: smoke thêm 52 kiểm tra (tổng 416/416, gồm vào/ra ca thật), unit thêm `StaffHrTests` (171/171).
- **Chưa xem trên trình duyệt.**

## Cho Huy

Hiệu suất đọc `CareTasks.AssignedToId / DueAt / Status`, `CareRecords.Status / PerformedAt` và `MedicationLogs.RecordedById / RecordedAt`: giữ nguyên tên các cột này. Trang chủ cá nhân đọc việc của chính nhân viên qua `StaffHrDAO.TasksForTodayAsync`.
