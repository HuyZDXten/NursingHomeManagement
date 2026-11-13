# S4-V1 · Hạ tầng AI + chatbot tư vấn công khai (AI-01, UC-20)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Must | 8 — **2 PR**: (1) hạ tầng, merge **trước 02/12** để Huy dùng; (2) chatbot công khai | 4 | S3-V1 (tạo yêu cầu, đặt tham quan) | `feature/s4-v1-ai-core`, `feature/s4-v1-public-bot` |

## Mục tiêu

Xây **hạ tầng AI dùng chung** cho cả nhóm (provider Gemini + provider giả để test, vòng lặp gọi tool, lưu hội thoại, rate limit, widget chat FE) và **chatbot tư vấn trên trang công khai**: trả lời về gói dịch vụ, giá, giường trống, giờ thăm, quy trình nhập viện; khi khách muốn thì tạo yêu cầu tư vấn hoặc đặt lịch tham quan. Chatbot **chỉ dùng dữ liệu từ tool**, không bịa giá, không tư vấn y tế.

## Tiêu chí hoàn thành

PR 1 — hạ tầng:
- [ ] `IAiChatProvider` với 2 cài đặt: `GeminiChatProvider` (REST, function calling) và `FakeAiChatProvider` (kịch bản cố định, dùng khi `Ai:Provider = "Fake"` hoặc `Ai:Enabled = false` ở Development/test).
- [ ] `AiChatEngine`: nạp 20 tin gần nhất, gọi provider, thực thi tool (tối đa 3 vòng), lưu tin nhắn + số token, trả câu trả lời. Lỗi provider → câu trả lời lịch sự "Trợ lý đang bận, vui lòng thử lại hoặc gọi hotline …", không ném 500.
- [ ] Interface `IAiTool` để mỗi người tự viết tool của module mình; tool nhận `CurrentUser?` và **tự kiểm quyền qua service có sẵn**.
- [ ] Widget `ChatWidget` (nút nổi góc phải, khung chat, lịch sử, đang gõ…) nhận hàm `send`/`loadHistory` qua props để Huy dùng lại.
- [ ] Unit test engine với `FakeAiChatProvider`: tool được gọi đúng tham số, kết quả tool được đưa lại cho model, tin nhắn được lưu.

PR 2 — chatbot công khai:
- [ ] Widget trên mọi trang công khai; khách không đăng nhập; hội thoại gắn với `conversationKey` (UUID lưu `localStorage`).
- [ ] Trả lời đúng giá gói, số giường trống, giờ thăm (lấy qua tool); câu hỏi y tế → khuyên liên hệ bác sĩ/điều dưỡng.
- [ ] Tạo yêu cầu tư vấn / đặt tham quan qua tool **chỉ sau khi khách xác nhận lại thông tin**; yêu cầu tạo từ chatbot có `Source = Chatbot` (S3-V1).
- [ ] Admin quản lý **tài liệu tri thức** (FAQ, quy trình nhập viện, chính sách thăm) tại `/staff/ai-knowledge`.
- [ ] Quá `RateLimit:AiMessagesPerMinute` mỗi IP → 429; `Ai:PublicChatbotEnabled = false` → widget ẩn.

## 1. Database — `01_Schema.sql` mục 9

```sql
IF OBJECT_ID(N'dbo.AiConversations', N'U') IS NULL
CREATE TABLE dbo.AiConversations (
    Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiConversations PRIMARY KEY,
    Channel       NVARCHAR(10)  NOT NULL,
    AccountId     INT           NULL CONSTRAINT FK_AiConversations_Accounts REFERENCES dbo.Accounts (Id),
    AnonymousKey  NVARCHAR(64)  NULL,
    Title         NVARCHAR(200) NULL,
    CreatedAt     DATETIME2     NOT NULL CONSTRAINT DF_AiConversations_CreatedAt DEFAULT (SYSUTCDATETIME()),
    LastMessageAt DATETIME2     NULL,
    CONSTRAINT CK_AiConversations_Channel CHECK (Channel IN (N'Public', N'Family', N'Staff')),
    CONSTRAINT CK_AiConversations_Owner CHECK (AccountId IS NOT NULL OR AnonymousKey IS NOT NULL)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AiConversations_AnonymousKey')
    CREATE UNIQUE INDEX UX_AiConversations_AnonymousKey ON dbo.AiConversations (AnonymousKey) WHERE AnonymousKey IS NOT NULL;
GO
IF OBJECT_ID(N'dbo.AiMessages', N'U') IS NULL
CREATE TABLE dbo.AiMessages (
    Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiMessages PRIMARY KEY,
    ConversationId BIGINT        NOT NULL CONSTRAINT FK_AiMessages_Conversations REFERENCES dbo.AiConversations (Id),
    Role           NVARCHAR(10)  NOT NULL,
    Content        NVARCHAR(MAX) NOT NULL,
    ToolName       NVARCHAR(100) NULL,
    InputTokens    INT           NULL,
    OutputTokens   INT           NULL,
    CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_AiMessages_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_AiMessages_Role CHECK (Role IN (N'user', N'assistant', N'tool'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AiMessages_Conversation')
    CREATE INDEX IX_AiMessages_Conversation ON dbo.AiMessages (ConversationId, Id);
GO
IF OBJECT_ID(N'dbo.AiKnowledgeDocs', N'U') IS NULL
CREATE TABLE dbo.AiKnowledgeDocs (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiKnowledgeDocs PRIMARY KEY,
    Title     NVARCHAR(200) NOT NULL,
    Content   NVARCHAR(MAX) NOT NULL,
    Tags      NVARCHAR(200) NULL,
    IsPublic  BIT           NOT NULL CONSTRAINT DF_AiKnowledgeDocs_IsPublic DEFAULT (1),
    IsActive  BIT           NOT NULL CONSTRAINT DF_AiKnowledgeDocs_IsActive DEFAULT (1),
    UpdatedAt DATETIME2     NOT NULL CONSTRAINT DF_AiKnowledgeDocs_UpdatedAt DEFAULT (SYSUTCDATETIME())
);
GO
```

`04_ReferenceData.sql`: 5–6 tài liệu mẫu (Quy trình nhập viện, Giờ thăm & quy định, Chính sách thanh toán, Dịch vụ y tế có/không có, Câu hỏi thường gặp).

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| Options | `Services/Ai/AiOptions.cs` | bind section `Ai` đã có trong `appsettings.json` (`Enabled, Provider, Gemini{ApiKey, Model}, Fallback{BaseUrl, ApiKey, Model}, MaxOutputTokens, Temperature, PublicChatbotEnabled`) |
| Hợp đồng | `Services/Ai/AiContracts.cs` | xem khối code bên dưới |
| Provider | `Services/Ai/GeminiChatProvider.cs` | `POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`, header `x-goog-api-key`; body `systemInstruction`, `contents` (role `user`/`model`, phần `functionCall`/`functionResponse`), `tools: [{ functionDeclarations: [...] }]`, `generationConfig { maxOutputTokens, temperature }`. Tham khảo cách dựng payload ở StreetBiz: `D:\StreetBiz\StreetBiz-BE\src\StreetBiz.Infrastructure\Services\Chatbot\ChatbotProviders.cs` → `GeminiChatbotModel` (dòng ~218–330) |
| Provider | `Services/Ai/FakeAiChatProvider.cs` | Nếu tin cuối là `user` và có tool tên khớp từ khóa ("giá" → `list_packages`, "giường" → `get_bed_availability`) thì trả tool call; nếu tin cuối là `tool` thì trả text tóm tắt kết quả. Đủ cho test và demo không key |
| Engine | `Services/Ai/AiChatEngine.cs` | vòng lặp tool, lưu DB qua `AiDAO`, cắt lịch sử 20 tin, đếm token |
| DAO | `AiDAO.cs` | `GetOrCreateConversationAsync(channel, accountId?, anonymousKey?)`, `ListMessagesAsync(conversationId, take)`, `AddMessagesAsync`, CRUD + `SearchKnowledgeAsync(keywords, publicOnly)` (tách từ khóa, `LIKE N'%…%'` trên Title/Content/Tags, xếp theo số từ khớp, lấy 3 tài liệu) |
| Tool công khai | `Services/Ai/Tools/PublicTools.cs` | `search_knowledge(query)`, `list_packages()`, `get_bed_availability()`, `get_tour_slots(from, to)`, `create_inquiry(fullName, phone, careNeeds, confirmed)`, `book_tour(fullName, phone, date, slot, guestCount, confirmed)` — gọi `IBillingService`, `IDashboardService`, `IAdmissionService` có sẵn |
| Service | `Services/Ai/PublicChatbotService.cs` | system prompt + bộ tool công khai → `AiChatEngine` |
| Controller | `AiController.cs` | endpoint công khai + quản lý tri thức; rate limit `AiPublic` (theo IP) |

```csharp
// Services/Ai/AiContracts.cs — hợp đồng dùng chung, CHỐT trong PR 1 để Huy code theo.
public sealed record AiMessage(string Role, string Content, string? ToolName = null, string? ToolCallId = null);
public sealed record AiToolDefinition(string Name, string Description, JsonObject ParametersSchema);
public sealed record AiToolCall(string Id, string Name, JsonElement Arguments);
public sealed record AiChatRequest(string SystemPrompt, IReadOnlyList<AiMessage> Messages, IReadOnlyList<AiToolDefinition> Tools);
public sealed record AiChatResult(string? Text, IReadOnlyList<AiToolCall> ToolCalls, int InputTokens, int OutputTokens);

public interface IAiChatProvider
{
    Task<AiChatResult> CompleteAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Một tool AI. Trả về JSON (chuỗi). PHẢI kiểm quyền bằng service có sẵn với user được truyền vào.</summary>
public interface IAiTool
{
    AiToolDefinition Definition { get; }
    Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default);
}

public sealed record AiReply(string Text, long ConversationId);

public interface IAiChatEngine
{
    Task<AiReply> ChatAsync(string channel, CurrentUser? user, string? anonymousKey, string message,
        string systemPrompt, IReadOnlyList<IAiTool> tools, CancellationToken cancellationToken = default);
}
```

System prompt (tiếng Việt, đặt trong `PublicChatbotService`), ý chính:
- Bạn là trợ lý tư vấn của <tên cơ sở>; trả lời ngắn (≤ 5 câu), thân thiện.
- Giá, giường trống, giờ thăm, quy trình: **luôn gọi tool**, không tự đoán; không có dữ liệu thì nói không biết và đưa hotline.
- Không chẩn đoán, không tư vấn thuốc/liều; câu hỏi y tế → khuyên trao đổi với bác sĩ của cơ sở khi tham quan.
- Không tiết lộ thông tin cư dân. Chỉ hỏi SĐT khi khách muốn được tư vấn/đặt lịch; **đọc lại thông tin và hỏi xác nhận** trước khi gọi `create_inquiry`/`book_tour` với `confirmed = true`.

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| GET | `/api/ai/public/config` | ẩn danh → `{enabled, greeting}` |
| POST | `/api/ai/public/chat` `{conversationKey, message}` | ẩn danh, rate limit `AiPublic` |
| GET | `/api/ai/public/history?conversationKey=` | ẩn danh |
| GET · POST · PUT · DELETE | `/api/ai/knowledge[/{id}]` | AdminOnly |

## 4. Frontend

- `src/components/ChatWidget.tsx` (dùng chung): props `{ title, greeting, send: (text) => Promise<string>, loadHistory?: () => Promise<{role, content}[]> }`; `FloatButton` mở `Card` cố định góc phải (rộng 360px, trên mobile toàn màn hình), danh sách tin (căn trái/phải), `Input.TextArea` + Enter gửi, trạng thái "Đang trả lời…", giới hạn 1000 ký tự.
- `src/api/ai.ts`: `publicChat`, `publicHistory`, `publicConfig`, CRUD tri thức.
- Gắn widget vào `PublicLayout` (`layouts/AppLayout.tsx`, file lõi của Viên) khi `config.enabled`.
- `src/pages/Operations/AiKnowledge.tsx` (`/staff/ai-knowledge`, AdminOnly): bảng + modal soạn tài liệu.

## 5. Cấu hình

`appsettings.Development.json` (không commit): `"Ai": { "Enabled": true, "Provider": "Fake" }` để chạy không cần key. Key thật: `dotnet user-secrets set "Ai:Gemini:ApiKey" "..."` và `"Ai:Gemini:Model"` theo model StreetBiz đang dùng; `Provider = "Gemini"`.

## 6. Kiểm thử

1. Provider Fake: hỏi "giá gói cơ bản?" → câu trả lời có 8.000.000 đ (lấy từ tool).
2. Provider Gemini (có key): hỏi giá, giường trống, "bà tôi bị tiểu đường dùng thuốc gì?" (phải từ chối tư vấn thuốc), đặt tham quan ngày mai buổi sáng → lễ tân thấy yêu cầu `Source = Chatbot`.
3. Gửi 25 tin trong 1 phút → 429.
4. Unit test `AiChatEngine` với Fake provider + tool giả: tool nhận đúng arguments, tin `tool` được lưu, tối đa 3 vòng.

## Bẫy cần tránh

- **Không** gửi dữ liệu cá nhân cư dân cho bên ngoài ở kênh công khai; tool công khai chỉ trả dữ liệu công khai.
- Không để model tự quyết tạo bản ghi: tool ghi dữ liệu phải có tham số `confirmed` và kiểm lại server-side.
- Lỗi/timeout Gemini (15 giây) phải trả câu xin lỗi, không treo request.
- Key AI chỉ ở user-secrets; không log nội dung key, không log toàn bộ prompt có dữ liệu cá nhân.
