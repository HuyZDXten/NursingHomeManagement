# S3-V6 · Chat gia đình – nhân viên (UC-106)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Should | 5 | 3.2 | (tùy chọn) S3-V2 để lọc người phụ trách | `feature/s3-v6-chat` |

## Mục tiêu

Gia đình nhắn tin hỏi tình hình người thân, nhân viên (Admin, Receptionist, Nurse và người phụ trách chính) trả lời; tin nhắn đến realtime qua SignalR, có đếm chưa đọc. Mỗi cặp (cư dân, tài khoản gia đình) là một cuộc trò chuyện.

## Tiêu chí hoàn thành

- [ ] Family mở "Tin nhắn", chọn người thân → thấy lịch sử, gửi tin; chỉ với cư dân mình được liên kết.
- [ ] Nhân viên thấy danh sách cuộc trò chuyện sắp theo tin mới nhất, số chưa đọc, trả lời được.
- [ ] Tin mới hiện ngay ở bên kia (không cần tải lại); mở cuộc trò chuyện thì đánh dấu đã đọc.
- [ ] Chuông thông báo khi có tin mới nếu người nhận không mở cửa sổ chat.
- [ ] Nội dung 1–2000 ký tự; gửi quá 20 tin/phút → 429.
- [ ] Family A không đọc được cuộc trò chuyện của Family B (thử đổi id → 404).

## 1. Database — `01_Schema.sql` mục 9

```sql
IF OBJECT_ID(N'dbo.Conversations', N'U') IS NULL
CREATE TABLE dbo.Conversations (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Conversations PRIMARY KEY,
    ResidentId      INT       NOT NULL CONSTRAINT FK_Conversations_Residents REFERENCES dbo.Residents (Id),
    FamilyAccountId INT       NOT NULL CONSTRAINT FK_Conversations_Accounts REFERENCES dbo.Accounts (Id),
    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_Conversations_CreatedAt DEFAULT (SYSUTCDATETIME()),
    LastMessageAt   DATETIME2 NULL,
    CONSTRAINT UQ_Conversations UNIQUE (ResidentId, FamilyAccountId)
);
GO
IF OBJECT_ID(N'dbo.Messages', N'U') IS NULL
CREATE TABLE dbo.Messages (
    Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Messages PRIMARY KEY,
    ConversationId INT            NOT NULL CONSTRAINT FK_Messages_Conversations REFERENCES dbo.Conversations (Id),
    SenderId       INT            NOT NULL CONSTRAINT FK_Messages_Accounts REFERENCES dbo.Accounts (Id),
    FromFamily     BIT            NOT NULL,
    Body           NVARCHAR(2000) NOT NULL,
    SentAt         DATETIME2      NOT NULL CONSTRAINT DF_Messages_SentAt DEFAULT (SYSUTCDATETIME()),
    ReadAt         DATETIME2      NULL,    -- bên kia đã đọc
    CONSTRAINT CK_Messages_Body CHECK (LEN(Body) > 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Messages_Conversation_SentAt')
    CREATE INDEX IX_Messages_Conversation_SentAt ON dbo.Messages (ConversationId, SentAt DESC);
GO
```

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | `Entities/OperationsChat.cs` | `Conversation` (nav `Resident`, `FamilyAccount`), `Message` |
| DAO | `ChatDAO.cs` | `GetOrCreateConversationAsync(residentId, familyId)` (bắt lỗi unique khi 2 request cùng tạo → đọc lại), `ListForFamilyAsync(familyId)`, `ListForStaffAsync(residentIds?)` kèm số chưa đọc (`Messages` `FromFamily = 1 AND ReadAt IS NULL`), `ListMessagesAsync(conversationId, beforeId?, take)`, `AddMessageAsync` (cập nhật `LastMessageAt` cùng `SaveChanges`), `MarkReadAsync(conversationId, readerIsFamily)` bằng `ExecuteUpdateAsync` |
| DTOs | `DTOs/Chat/ChatDtos.cs` | `ConversationDto {id, residentId, residentName, familyName, lastMessage, lastMessageAt, unread}`, `MessageDto {id, conversationId, senderName, fromFamily, body, sentAt, readAt}`, `SendMessageRequest {body}` |
| Service | `ChatService.cs` | Quy tắc bên dưới |
| Realtime | `Services/Interfaces/IOperationsServices.cs` → `IChatPublisher`; API cài bằng `IHubContext<NotificationHub>` gửi sự kiện **`chat`** | Dùng lại hub `/hubs/notifications` có sẵn, không cần hub mới |
| Controller | `ConversationsController.cs` | rate limit policy mới `Chat` (20/phút theo user) |

Quy tắc quyền:

- Family: chỉ cư dân có `ResidentFamilyLinks` với mình (không cần cờ quyền cụ thể).
- Nhân viên được đọc/trả lời: Admin, Receptionist, Nurse; Caregiver chỉ khi là người phụ trách chính (S3-V2) — nếu S3-V2 chưa xong thì bỏ Caregiver.
- Người nhận khi gia đình gửi: nhân viên phụ trách chính + mọi Nurse đang có ca hôm nay (nếu có S3-V2), nếu không thì mọi Receptionist. Khi nhân viên gửi: tài khoản gia đình của cuộc trò chuyện.
- Gửi xong: publisher đẩy `chat` (MessageDto) cho người nhận; đồng thời `NotifyAsync` loại `System` "Tin nhắn mới từ …" (chuông).

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| GET | `/api/conversations` | đăng nhập (Family: của mình; nhân viên: theo quyền) |
| POST | `/api/conversations` `{residentId}` | FamilyOnly (mở hoặc lấy lại cuộc trò chuyện) |
| GET | `/api/conversations/{id}/messages?beforeId=&take=30` | đăng nhập + kiểm quyền |
| POST | `/api/conversations/{id}/messages` | đăng nhập + kiểm quyền, rate limit `Chat` |
| POST | `/api/conversations/{id}/read` | đăng nhập + kiểm quyền |

## 4. Frontend

- `src/hooks/useChat.ts`: kết nối `/hubs/notifications` (giống `useNotifications`), lắng nghe sự kiện `chat`, trả về `onMessage` callback.
- `src/pages/Operations/Messages.tsx` dùng chung cho nhân viên và gia đình: bố cục 2 cột (danh sách cuộc trò chuyện `List` + khung tin nhắn), cuộn lên tải tin cũ (`beforeId`), ô nhập + nút Gửi (Enter để gửi, Shift+Enter xuống dòng), tự đánh dấu đã đọc khi mở.
- Family: nút "Nhắn tin cho cơ sở" trên thẻ người thân trong `FamilyHome` (file của Huy — nhắn Huy thêm 1 nút) hoặc chọn người thân ngay trong trang Tin nhắn.
- Route `/staff/messages` (Admin, Receptionist, Nurse, Caregiver) và `/family/messages` (Family), menu "Tin nhắn" kèm `Badge` số chưa đọc (tùy chọn).

## 5. Dữ liệu demo

1 cuộc trò chuyện NH-0001 ↔ family (3 tin, tin cuối của gia đình chưa đọc).

## 6. Kiểm thử

1. Mở 2 trình duyệt: `family` và `nurse`. Family gửi → nurse thấy ngay + chuông; nurse trả lời → family thấy ngay.
2. `family2` gọi `GET /api/conversations/1/messages` → 404.
3. `accountant` gọi `GET /api/conversations` → 403 (không thuộc nhóm nhân viên được chat).
4. Smoke test: family tạo cuộc trò chuyện + gửi tin; family2 bị chặn.

## Tham khảo code có sẵn

`hooks/useNotifications.ts`, `Hubs/NotificationHub.cs` (`SignalRNotificationPublisher`), `ResidentAccessService` (kiểm liên kết Family).

## Bẫy cần tránh

- SignalR gửi theo user id = claim `sub` (đã cấu hình `SubjectUserIdProvider`), nên `Clients.Users(ids.Select(i => i.ToString()))`.
- Không trả email/SĐT của nhân viên cho gia đình trong DTO; chỉ tên hiển thị.
