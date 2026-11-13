# S5-H1 · Hoàn thiện module Chăm sóc & Gia đình, kiểm thử, video dự phòng

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Huy | Must | 5 | 5 (07–13/12) | mọi task Sprint 3–4 | sửa lỗi trên `feature/s5-h1-fixes` → `develop` |

## Mục tiêu

Luồng của Doctor, Nurse, Caregiver và Family chạy ổn định, dữ liệu demo đầy đủ, kiểm thử tự động bao phủ quy tắc quan trọng của module, có video demo ghi sẵn phòng khi demo trực tiếp gặp sự cố.

## Việc cần làm

**Thứ Hai–Thứ Ba: QA bằng trình duyệt thật** (desktop + tablet 768px + điện thoại cho trang Family).

| Role | Màn hình cần đi hết | Kiểm |
| --- | --- | --- |
| Doctor | Hồ sơ cư dân (bệnh nền, dị ứng, khám, ngưỡng), lịch thuốc, đánh giá | Cảnh báo dị ứng hiện đúng |
| Nurse | Công việc, kế hoạch chăm sóc, sự cố, phát thuốc, duyệt nhật ký AI, đánh giá đến hạn | Nhận chuông sinh hiệu/sự cố realtime |
| Caregiver | Việc của tôi, ghi nhận hằng ngày, sự cố, phát thuốc | Không xem được cư dân không được giao (URL trực tiếp → 403) |
| Family / family2 | Người thân, nhật ký, sự cố chia sẻ, lịch thăm, góp ý, trợ lý AI, chat | family2 không thấy thuốc; không xem được NH-0001 |

**Thứ Tư: dữ liệu & kiểm thử**
- [ ] `05_DemoSeed.sql` mục 11: mọi màn có dữ liệu (kế hoạch chăm sóc, bữa ăn 3 ngày gần nhất có chia sẻ, đánh giá đến hạn, lịch thăm ngày mai, góp ý, nhật ký AI đã duyệt hôm qua).
- [ ] Unit test: `AssessmentScales`, `VisitingHours.Slots`, `VitalTrends.Analyze`, `TextMatch.Normalize`, chọn ngày trong tuần của kế hoạch chăm sóc.
- [ ] `scripts/smoke-test.mjs` có check cho mọi task H và xanh.
- [ ] `.\Database\setup-local-db.ps1 -Recreate` trên máy **Viên** chạy sạch.

**Thứ Năm (11/12): đóng băng tính năng**, review và approve PR `develop` → `main` của Viên.

**Thứ Sáu (12/12): tổng duyệt** + **ghi video dự phòng** 10 phút theo kịch bản ở [S5-V1](S5-V1-hoan-thien.md) (OBS, 1080p, có tiếng thuyết minh), lưu Google Drive nhóm.

**Tài liệu**
- [ ] Cập nhật cột Trạng thái trong `docs/tasks/README.md` và `docs/SPRINTS.md`.
- [ ] Bổ sung vào `docs/HANDOVER.md` mục "Đã làm và chưa làm" các giới hạn của module Chăm sóc (vd. cảnh báo dị ứng chỉ so khớp tên).

## Bảng lỗi QA

| # | Màn | Role | Mô tả | Mức | Người sửa | Trạng thái |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | | | | | | |
