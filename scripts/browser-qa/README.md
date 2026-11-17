# QA giao diện bằng trình duyệt thật (Edge/Chrome headless)

Bổ sung cho `scripts/smoke-test.mjs` (kiểm API): chạy FE thật trong trình duyệt để bắt lỗi mà build/lint không thấy
(trang vỡ khi render, lỗi console, API trả 4xx/5xx bất ngờ, bảng tràn ngang). Dùng cho bước "QA bằng trình duyệt" của S5-V1.

## Chuẩn bị (một lần)

```powershell
cd scripts\browser-qa
npm install        # chỉ cài playwright-core (~2 MB), KHÔNG tải trình duyệt: dùng Microsoft Edge có sẵn trên Windows
```

## Chạy

Cần API (cổng 5080) + FE (cổng 5173) đang chạy trên DB đã dựng bằng `Database/setup-local-db.ps1`:

```powershell
# cửa sổ 1 — API: tắt AI thật và job nền để kết quả ổn định
cd BE; $env:Ai__Provider="Fake"; $env:RateLimit__CredentialsPerMinute=500; dotnet run --project NursingHome.API --launch-profile http
# cửa sổ 2 — FE
cd FE; npm run dev
# cửa sổ 3 — QA
cd scripts\browser-qa
node sweep.mjs            # duyệt mọi route × 8 vai trò (Khách, Admin, Lễ tân, Kế toán, Bác sĩ, Điều dưỡng, Hộ lý, Gia đình)
node sweep.mjs Admin      # chỉ một vai trò
node flows.mjs            # 13 luồng tương tác của BL-V5 (đăng bài, cấu hình, thông báo...)
node chat.mjs             # chat gia đình – nhân viên realtime bằng hai trình duyệt: tin mới, "Đã xem", "đang nhập…", nối lại sau khi mất mạng
```

Đổi địa chỉ bằng biến môi trường `FE_BASE`, `API_BASE`. Ảnh chụp màn hình nằm ở `shots/` (không commit).

## Đọc kết quả

- `sweep.mjs` in từng vai trò: số lỗi console / `pageerror` / HTTP ≥ 400 (trừ 401) / render hỏng, và số route bị chuyển sang `/forbidden` hoặc `/login` (đúng kỳ vọng khi vai trò không được vào).
- Lỗi SignalR "connection was stopped during negotiation" được bỏ qua: do trình duyệt chuyển trang khi kết nối đang mở.
- Cảnh báo `antd: List` còn lại là cảnh báo deprecated cho phiên bản antd sau (trang Tin nhắn, Ca trực, chuông thông báo).

## Lưu ý

- `flows.mjs` **ghi dữ liệu thật** (bài viết, thông báo, danh mục, cấu hình; có tải một ảnh PNG 1×1 lên kho ảnh đang cấu hình, tức là Cloudinary nếu đã có khóa). Script tự dọn bài viết/danh mục/cấu hình nhưng **thông báo và nhật ký thao tác thì còn lại**; nên chạy trên DB test hoặc dựng lại DB sau đó.
- `chat.mjs` gửi các tin thử bắt đầu bằng `QA-RT` vào cuộc trò chuyện số 1 (NH-0001, `family` và `nurse`) và **không tự xóa**; dọn bằng `DELETE FROM dbo.Messages WHERE Body LIKE N'QA-RT%'` (sqlcmd cần `-I`). Bài "mất kết nối" dùng chế độ offline của trình duyệt nên mất khoảng 5–25 giây để SignalR nối lại.
- Chưa kiểm ở màn hình điện thoại cho trang quản trị (chỉ trang công khai và trang gia đình kiểm ở 390 px, trang quản trị kiểm ở 768 px).
