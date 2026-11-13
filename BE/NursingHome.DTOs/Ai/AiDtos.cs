using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Ai;

// MODULE VẬN HÀNH (Viên) — S4-V1 chatbot tư vấn công khai + tri thức AI.

public class AiPublicConfigDto
{
    public bool Enabled { get; set; }
    public string Greeting { get; set; } = "";
}

public class AiPublicChatRequest
{
    /// <summary>UUID do trình duyệt sinh và lưu localStorage (khách không đăng nhập).</summary>
    [Required]
    [RegularExpression("^[0-9a-fA-F-]{32,36}$", ErrorMessage = "conversationKey phải là UUID.")]
    public string ConversationKey { get; set; } = "";

    [Required(ErrorMessage = "Nhập câu hỏi.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Câu hỏi 1–1000 ký tự.")]
    public string Message { get; set; } = "";
}

public class AiChatReplyDto
{
    public string Reply { get; set; } = "";
}

/// <summary>Một tin hiện lại trong khung chat. Role: user | assistant.</summary>
public class AiHistoryItemDto
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

public class AiKnowledgeDocDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? Tags { get; set; }
    public bool IsPublic { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveAiKnowledgeRequest
{
    [Required(ErrorMessage = "Nhập tiêu đề.")]
    [MaxLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Nhập nội dung.")]
    [StringLength(20000, MinimumLength = 10, ErrorMessage = "Nội dung 10–20000 ký tự.")]
    public string Content { get; set; } = "";

    /// <summary>Từ khóa cách nhau bằng dấu phẩy (giúp tìm kiếm).</summary>
    [MaxLength(200)]
    public string? Tags { get; set; }

    /// <summary>false = chỉ trợ lý gia đình/nội bộ dùng.</summary>
    public bool IsPublic { get; set; } = true;

    public bool IsActive { get; set; } = true;
}

/// <summary>S4-V2 (AI-12): câu hỏi của nhân viên cho trợ lý số liệu.</summary>
public class AiStaffChatRequest
{
    [Required(ErrorMessage = "Nhập câu hỏi.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Câu hỏi 1–1000 ký tự.")]
    public string Message { get; set; } = "";
}
