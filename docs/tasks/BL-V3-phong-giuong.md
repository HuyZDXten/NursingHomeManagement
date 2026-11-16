# BL-V3 · Loại phòng & phụ thu, giữ giường, báo hỏng & sửa chữa (UC-36, UC-41, UC-42)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Could (backlog) | 6 | 17/11 | BL-V2 | `feature/rooms-beds` |

## Mục tiêu

Phòng có loại và phụ thu tính vào hóa đơn; lễ tân giữ giường cho khách sắp nhập viện; nhân viên báo hỏng, Admin theo dõi sửa chữa và giường hỏng không bị xếp người.

## Kết quả triển khai (17/11/2026)

- [x] **UC-36 loại phòng** (bảng `RoomTypes`, cột `Rooms.RoomTypeId`): Admin quản lý loại + phụ thu/ngày ở trang "Vận hành phòng"; gán loại cho phòng trong form phòng ở trang Phòng & giường.
    - `Rooms.RoomTypeId` thêm bằng `IF COL_LENGTH(...) IS NULL ALTER TABLE` ở schema mục 9.9 (không sửa khối CREATE của bảng lõi), nên `setup-local-db.ps1` không `-Recreate` cũng thêm được cột cho DB đang có dữ liệu — không cần script trong `upgrades/`.
    - Hóa đơn tháng (`BillingService.CreateInvoiceAsync`, cả hóa đơn hàng loạt) thêm dòng "Phụ thu {loại} (phòng {số}): dd/MM–dd/MM (n ngày)", số lượng = số ngày, đơn giá = phụ thu/ngày lúc lập hóa đơn.
    - Đếm ngày ở: từ ngày vào phòng tới hết ngày trước ngày rời phòng; ngày chuyển phòng tính cho phòng mới (không trùng); lần ở chưa kết thúc tính tới hết kỳ (`RoomRules.SurchargeLines`).
    - Seed: mọi phòng = Tiêu chuẩn (0 đ, hóa đơn cũ không đổi), phòng 202 (đang trống) = Phòng đơn VIP 150.000 đ/ngày.
- [x] **UC-41 giữ giường** (bảng `BedReservations`): lễ tân giữ giường trống tối đa 7 ngày (giữ tới hết ngày chọn), có thể gắn với yêu cầu tư vấn (S3-V1).
    - Giường đang giữ chỉ xếp được cho cư dân nhập viện từ đúng yêu cầu đã giữ (`BedReservationGuard` chạy trong transaction của `RoomDAO.AssignAsync` và tái nhập viện); khi đó lượt giữ chuyển "Đã nhập viện". Hết hạn thì tự coi là trả, lần xếp kế tiếp đóng lượt giữ.
    - Sơ đồ phòng gắn nhãn "Đang giữ: tên" trên giường; `BedDto` có `reservedFor`, `reservedUntil`.
    - Không thêm trạng thái mới vào enum `BedStatus` lõi. Chatbot công khai vẫn đếm giường đang giữ là trống (chấp nhận, ghi chú).
- [x] **UC-42 báo hỏng** (bảng `MaintenanceRequests`): mọi nhân viên báo (phòng, giường tùy chọn, mức độ); mức Khẩn gửi thông báo cho Admin.
    - "Khóa giường" + giường đang trống → giường chuyển Bảo trì; xong/hủy phiếu và không còn phiếu khóa nào khác → giường trống lại. Giường có người ở thì không đổi trạng thái.
    - Luồng chỉ đi tới: Mới báo → Đang sửa → Đã xong; Mới báo/Đang sửa → Đã hủy (`MaintenanceFlow`). Người báo nhận thông báo khi sửa xong.
- Trang mới `/staff/room-ops` "Vận hành phòng": 3 tab Báo hỏng (mọi nhân viên), Giữ giường (lễ tân), Loại phòng (Admin).
- Kiểm thử: smoke thêm 23 kiểm tra (tổng 293/293), unit thêm `RoomOpsTests` (123/123).
- **Chưa xem trên trình duyệt.**
