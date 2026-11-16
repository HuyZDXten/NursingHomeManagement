# BL-V1 · Ảnh đại diện & ảnh cư dân, đăng nhập Google, mã mời gia đình (UC-07, UC-24, UC-02, UC-08)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Could (backlog) | 9 | 16/11 | — | `feature/accounts-media` |

## Mục tiêu

Người dùng có ảnh đại diện, cư dân có ảnh chân dung; nhân viên đăng nhập được bằng Google; lễ tân mời người thân tự kích hoạt tài khoản gia đình mà không phải đặt mật khẩu hộ.

## Kết quả triển khai (16/11/2026)

- [x] **UC-07, UC-24 ảnh:** `POST/DELETE /api/auth/me/avatar`, `POST/DELETE /api/residents/{id}/photo` (FrontDesk), `POST /api/uploads/image?folder=images|care` (Staff, trả URL).
    - Chỉ nhận JPG/PNG/WebP ≤ 5 MB. Định dạng xác định bằng chữ ký đầu file (`Helpers/ImageRules.cs`), đuôi lưu theo chữ ký nên file HTML đổi tên `.png` bị từ chối.
    - Đủ 3 khóa Cloudinary → `CloudinaryImageStorage` (thư mục `nursinghome/{folder}`); thiếu → `API/Storage/LocalImageStorage` lưu `wwwroot/uploads` (đã `.gitignore`), phục vụ ở `/uploads/...`.
    - DB lưu đường dẫn tương đối `/uploads/...` để đổi cổng/máy không hỏng ảnh; FE ghép địa chỉ API bằng `utils/media.ts`.
    - FE: component `PhotoPicker` ở trang Hồ sơ cá nhân và đầu trang chi tiết cư dân (chỉ lễ tân/Admin đổi được).
- [x] **UC-02 Google:** `GET /api/auth/config` trả Client ID (rỗng = ẩn nút), `POST /api/auth/google {idToken}`.
    - `GoogleTokenValidator` (chép từ ASS2) kiểm chữ ký, hạn, audience = Client ID. Chỉ đăng nhập tài khoản **đã có** cùng email, email Google phải đã xác minh; không tự tạo tài khoản.
    - Client ID đặt ở user-secrets (`Google:ClientId`), FE lấy qua `/api/auth/config` nên không cần `VITE_GOOGLE_CLIENT_ID`.
    - OAuth client trên Google Cloud Console phải có `http://localhost:5173` trong Authorized JavaScript origins (client của ASS2 đang có 5174).
- [x] **UC-08 mã mời:** bảng `FamilyInvitations` (schema 9.7). Lễ tân tạo trong tab "Tài khoản gia đình" của hồ sơ cư dân; người thân mở `/kich-hoat?code=...`, đặt họ tên + mật khẩu → tạo tài khoản Family, liên kết cư dân theo quyền đã chọn, đăng nhập luôn.
    - Mã 24 ký tự base64url, chỉ lưu SHA-256; mã + link chỉ hiện một lần lúc tạo. Hạn 7 ngày, dùng một lần, tạo mã mới cho cùng email + cư dân thì mã cũ tự thu hồi.
    - Kích hoạt chạy trong một transaction Serializable (`FamilyInvitationDAO.AcceptAsync`): hai người dùng cùng mã thì chỉ một bên thành công.
    - Có SMTP thì gửi email link; lỗi gửi không làm hỏng việc tạo mã (`emailSent = false`, lễ tân chép link).
    - Seed 10.7: mã demo `DEMO-NH0003-MOI` cho NH-0003 (hạn 30 ngày từ lúc seed).
- Sửa lõi: `ExceptionHandlingMiddleware` trả 413/400 cho `BadHttpRequestException` (upload quá cỡ trước đây ra 500); `Program.cs` thêm `AddImageStorage()` + `UseUploadedImages()`; `IAuthService` thêm `GoogleLoginAsync`, `GetPublicConfig`.
- Kiểm thử: smoke thêm 25 kiểm tra (ảnh hợp lệ/giả/quá cỡ, phục vụ tĩnh, phân quyền, Google 503 khi chưa cấu hình, vòng đời mã mời); unit thêm `AccountMediaTests` (102/102).
- **Chưa thử:** đăng nhập Google thật và Cloudinary thật trên trình duyệt (smoke chạy với khóa bị xóa trắng).

## Cho Huy

Ảnh đính kèm ghi chép chăm sóc (UC-60): gửi `multipart/form-data` trường `file` tới `POST /api/uploads/image?folder=care`, lưu `url` trả về vào bảng của mình; hiển thị bằng `mediaUrl(url)` trong `FE/src/utils/media.ts`.
