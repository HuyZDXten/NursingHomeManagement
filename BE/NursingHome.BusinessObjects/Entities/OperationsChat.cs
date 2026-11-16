namespace NursingHome.BusinessObjects.Entities;

// MODULE VẬN HÀNH (Viên) — S3-V6 chat gia đình – nhân viên (UC-106). Bảng: 01_Schema.sql mục 9.5.

/// <summary>Một cuộc trò chuyện cho mỗi (cư dân, tài khoản gia đình); nhân viên được phép cùng trả lời.</summary>
public class Conversation
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public int FamilyAccountId { get; set; }

    public Account FamilyAccount { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>Bảng dbo.Messages. ReadAt = lúc bên kia (gia đình ↔ nhân viên) đọc.</summary>
public class ChatMessage
{
    public long Id { get; set; }

    public int ConversationId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public int SenderId { get; set; }

    public Account Sender { get; set; } = null!;

    public bool FromFamily { get; set; }

    public string Body { get; set; } = "";

    public DateTime SentAt { get; set; }

    public DateTime? ReadAt { get; set; }
}
