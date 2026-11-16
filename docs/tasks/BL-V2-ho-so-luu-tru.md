# BL-V2 · Hồ sơ lưu trú: giấy tờ & hợp đồng, tạm vắng, tái nhập viện, tiền cọc (UC-29, UC-30, UC-31, UC-33, UC-99)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Could (backlog) | 10 | 16/11 | BL-V1 | `feature/resident-records` |

## Mục tiêu

Lễ tân lưu giấy tờ và hợp đồng của cư dân, ghi nhận các lần tạm vắng, tái nhập viện cho người đã ra; kế toán thu và hoàn tiền cọc.

## Kết quả triển khai (16/11/2026)

- [x] **UC-29 giấy tờ** (bảng `ResidentDocuments`): CCCD, BHYT, giấy tờ y tế. PDF/JPG/PNG ≤ 10 MB, nhận dạng bằng chữ ký file.
    - File lưu **riêng tư** ở `App_Data/documents` của API (ngoài `wwwroot`, đã `.gitignore`), chỉ tải qua endpoint có kiểm quyền, luôn ở dạng tải về. Không đưa lên Cloudinary vì là dữ liệu nhạy cảm.
    - Chỉ lễ tân và Admin xem/tải/xóa.
- [x] **UC-30 hợp đồng** (bảng `ResidentContracts`): số, ngày ký, hiệu lực, giá tháng, bản scan không bắt buộc. Tối đa một hợp đồng Active mỗi cư dân (`UX_ResidentContracts_Active`).
    - Xem/tải theo quyền phần Billing của cư dân: lễ tân, kế toán, Admin, và **gia đình có quyền xem hóa đơn** (API đã mở; trang gia đình là của Huy nên chưa có nút).
    - Trùng số hợp đồng hoặc đã có hợp đồng hiệu lực → 409, file vừa lưu được xóa để không mồ côi.
- [x] **UC-31 tạm vắng** (bảng `ResidentLeaves`): về nhà, nằm viện, khác; tối đa một lần vắng đang mở (`UX_ResidentLeaves_Open`).
    - Lễ tân, điều dưỡng, Admin ghi nhận đi/về; không cho thời điểm đi ở tương lai, ngày dự kiến về không trước ngày đi.
    - Nhãn "Tạm vắng" ở danh sách và trang chi tiết cư dân (đỏ khi quá ngày dự kiến về).
    - Chỉ đánh dấu: **không** tự hủy việc chăm sóc, lịch thuốc (phần của Huy) và không đổi cách tính hóa đơn. Huy có thể dùng `GET /api/resident-leaves/open` nếu muốn ẩn việc của cư dân đang vắng.
- [x] **UC-33 tái nhập viện** (bảng `ResidentStays`): nút "Tái nhập viện" ở hồ sơ cư dân đã ngừng lưu trú.
    - Một transaction Serializable: lưu lần lưu trú cũ vào `ResidentStays`, **đóng các gói dịch vụ còn mở tại ngày ngừng lưu trú** (ngừng lưu trú hiện không đóng gói, nếu không đóng sẽ tính tiền trùng), chuyển cư dân về Active, xếp giường trống, mở gói mới.
    - Ngày bắt đầu gói không trước ngày ngừng lưu trú, không quá 30 ngày tới.
- [x] **UC-99 tiền cọc** (bảng `Deposits`): tối đa một khoản đang giữ (`UX_Deposits_Held`). Kế toán/Admin thu; chỉ hoàn sau khi cư dân ngừng lưu trú, số hoàn ≤ số cọc; form hoàn hiện công nợ chưa trả và gợi ý số hoàn = cọc − công nợ, kế toán tự quyết và ghi chú khoản trừ.
- Seed 10.8: hợp đồng HD-2026-0001 của NH-0001, NH-0004 đang nằm viện, cọc 10 triệu của NH-0001 và 8 triệu chưa hoàn của NH-0005 (đã ngừng lưu trú).
- Thêm policy `LeaveRecorders` (Admin, Receptionist, Nurse).
- Kiểm thử: smoke thêm 32 kiểm tra (tổng 270/270), unit thêm `ResidentRecordsTests` (111/111).
- **Chưa xem trên trình duyệt.**
