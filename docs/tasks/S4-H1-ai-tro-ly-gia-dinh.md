# S4-H1 · Trợ lý AI cho gia đình (AI-02)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Huy | Must | 5 | 4 | S4-V1 PR 1 (hợp đồng `IAiTool`, `IAiChatEngine`, `ChatWidget`) — trong lúc chờ, viết tool + test với `FakeAiChatProvider` | `feature/s4-h1-family-assistant` |

## Mục tiêu

Người nhà hỏi bằng tiếng Việt tự nhiên — "Hôm nay mẹ tôi ăn uống thế nào?", "Huyết áp tuần này của bố?", "Tháng này còn nợ bao nhiêu?", "Lịch thăm của tôi khi nào?" — và nhận câu trả lời ngắn dựa trên **đúng dữ liệu họ được phép xem**. Trợ lý không chẩn đoán, không bình luận thuốc; khi lo ngại thì khuyên liên hệ điều dưỡng (gợi ý dùng mục Tin nhắn).

## Tiêu chí hoàn thành

- [ ] Widget trợ lý hiện ở mọi trang của tài khoản Family (góc phải), giữ lịch sử hội thoại theo tài khoản.
- [ ] Tool chỉ trả dữ liệu qua **service có sẵn với `CurrentUser` của người hỏi** → quyền tự động đúng: không có `CanViewMedication` thì tool thuốc trả "không có quyền xem" và trợ lý nói rõ điều đó.
- [ ] Người nhà có nhiều người thân: trợ lý hỏi lại "Anh/chị muốn hỏi về ai?" nếu câu hỏi không rõ.
- [ ] `family2` hỏi về NH-0001 (không liên kết) → trợ lý trả lời không có thông tin.
- [ ] Câu hỏi y tế ("có nên tăng liều không?") → từ chối nhẹ nhàng, khuyên trao đổi với bác sĩ/điều dưỡng.
- [ ] Rate limit theo user (`RateLimit:AiMessagesPerMinute`).

## 1. Database

Không bảng mới (dùng `AiConversations` kênh `Family`, `AiMessages` của S4-V1).

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| Tools | `Services/Ai/Tools/FamilyTools.cs` | Bảng tool bên dưới; mỗi tool là một class `IAiTool` |
| Service | `Services/Ai/FamilyAssistantService.cs` | System prompt + bộ tool → `IAiChatEngine.ChatAsync("Family", user, null, message, prompt, tools)` |
| DI | `ServiceCollectionExtensions.Care.cs` | đăng ký tool + service |
| Controller | `FamilyAssistantController.cs` | `[Authorize(Policy = FamilyOnly)]` |

| Tool | Tham số | Gọi service | Trả về (JSON gọn) |
| --- | --- | --- | --- |
| `list_my_residents` | — | `IFamilyService.MyResidentsAsync` | id, tên, quyền xem |
| `get_day_summary` | `residentId`, `date` (mặc định hôm nay) | `FamilyTimelineService` (S3-H6) | các mục nhật ký đã chia sẻ |
| `get_recent_vitals` | `residentId`, `days` (≤ 14) | `IHealthService.ListAsync` (tự lọc `ShareWithFamily` cho Family) | ngày, HA, mạch, nhiệt độ, SpO2 |
| `get_active_medications` | `residentId` | `IFamilyService.DetailsAsync` → `Medications` | tên thuốc, giờ (không liều nếu không cần) |
| `get_open_invoices` | `residentId` | `IFamilyService.DetailsAsync` → `Invoices` còn nợ | số, kỳ, còn nợ, hạn |
| `get_my_visits` | — | `VisitService` (S3-H3) | lịch sắp tới |

Mỗi tool bắt `ForbiddenException`/`NotFoundException` và trả `{"error":"Bạn không có quyền xem mục này"}` để model diễn đạt lại, không để lỗi nổi lên thành 500.

System prompt, ý chính:
- Bạn là trợ lý của <tên cơ sở> giúp người nhà nắm tình hình người thân. Trả lời ấm áp, ≤ 6 câu, dùng "ông/bà" theo giới tính cư dân.
- **Chỉ** nói điều có trong kết quả tool; thiếu dữ liệu thì nói chưa có ghi nhận.
- Không chẩn đoán, không đánh giá số liệu là "nguy hiểm/bình thường", không khuyên dùng/ngừng thuốc. Khi người nhà lo lắng → khuyên nhắn điều dưỡng qua mục Tin nhắn hoặc gọi hotline.
- Không trả lời về cư dân khác.

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| POST | `/api/ai/family/chat` `{message}` | FamilyOnly + rate limit |
| GET | `/api/ai/family/history` | FamilyOnly |

## 4. Frontend

- `src/api/familyAssistant.ts`.
- `src/pages/Family/FamilyAssistant.tsx`: bọc `ChatWidget` (của S4-V1) với `title="Trợ lý gia đình"`, lời chào + 3 câu hỏi gợi ý (chip bấm được).
- Gắn vào layout: nhắn Viên thêm 1 dòng trong `AppLayout` — `{user?.role === 'Family' && <FamilyAssistant />}` (file lõi).

## 5. Kiểm thử

1. `family` hỏi "Hôm qua bố tôi ăn uống thế nào?" → trả lời khớp `MealLogs` đã chia sẻ.
2. `family2` hỏi "Mẹ tôi đang uống thuốc gì?" (không có quyền thuốc) → nói không có quyền xem.
3. `family2` hỏi về "ông Nguyễn Văn An" → không có thông tin.
4. Unit test từng tool với service giả: tool không bao giờ gọi repository trực tiếp, luôn truyền `CurrentUser`.
5. Fake provider: kịch bản gọi `get_day_summary` → engine trả text.

## Bẫy cần tránh

- **Không** viết tool truy vấn DAO/Repository trực tiếp — sẽ bỏ qua `ResidentAccessService` và lộ dữ liệu. Luôn đi qua service.
- `residentId` do model đưa ra không đáng tin: service kiểm quyền sẽ chặn; đừng "sửa" bằng cách tự chọn người thân đầu tiên.
