namespace NursingHome.BusinessObjects.Entities;

// MODULE VẬN HÀNH (Viên) — S4-V1 hạ tầng AI dùng chung (AI-01, UC-20). Bảng: 01_Schema.sql mục 9.6.
// Huy dùng lại cho trợ lý gia đình (S4-H1, kênh Family) và nhật ký AI (S4-H2).

public static class AiChannels
{
    public const string Public = "Public";
    public const string Family = "Family";
    public const string Staff = "Staff";
}

public static class AiRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string Tool = "tool";
}

/// <summary>Hội thoại với trợ lý AI. Public: theo AnonymousKey; Family/Staff: mỗi tài khoản một hội thoại mỗi kênh.</summary>
public class AiConversation
{
    public long Id { get; set; }

    public string Channel { get; set; } = AiChannels.Public;

    public int? AccountId { get; set; }

    public string? AnonymousKey { get; set; }

    public string? Title { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }
}

/// <summary>Bảng dbo.AiMessages: tin user/assistant + kết quả tool (role "tool") để kiểm tra lại câu trả lời.</summary>
public class AiMessageRecord
{
    public long Id { get; set; }

    public long ConversationId { get; set; }

    public string Role { get; set; } = AiRoles.User;

    public string Content { get; set; } = "";

    public string? ToolName { get; set; }

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Tài liệu tri thức (FAQ, quy trình, chính sách) cho tool search_knowledge.</summary>
public class AiKnowledgeDoc
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string Content { get; set; } = "";

    public string? Tags { get; set; }

    /// <summary>false = chỉ kênh nội bộ/gia đình được tra cứu.</summary>
    public bool IsPublic { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; }
}
