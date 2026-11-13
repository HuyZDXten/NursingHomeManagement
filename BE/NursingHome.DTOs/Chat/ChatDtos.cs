using System.ComponentModel.DataAnnotations;

namespace NursingHome.DTOs.Chat;

// MODULE VẬN HÀNH (Viên) — S3-V6 chat gia đình – nhân viên (UC-106).
// Không trả email/SĐT của nhân viên cho gia đình: chỉ tên hiển thị + vai trò.

public class ConversationDto
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentCode { get; set; } = "";
    public string ResidentName { get; set; } = "";
    public int FamilyAccountId { get; set; }
    public string FamilyName { get; set; } = "";
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }

    /// <summary>Tin của bên kia mà người xem chưa đọc.</summary>
    public int Unread { get; set; }
}

public class ChatMessageDto
{
    public long Id { get; set; }
    public int ConversationId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = "";

    /// <summary>Role của người gửi (để gia đình biết đang nói chuyện với điều dưỡng, lễ tân…).</summary>
    public string SenderRole { get; set; } = "";
    public bool FromFamily { get; set; }
    public string Body { get; set; } = "";
    public DateTime SentAt { get; set; }

    /// <summary>Bên kia đã đọc lúc.</summary>
    public DateTime? ReadAt { get; set; }
}

public class OpenConversationRequest
{
    [Range(1, int.MaxValue)]
    public int ResidentId { get; set; }
}

public class SendMessageRequest
{
    [Required(ErrorMessage = "Nhập nội dung tin nhắn.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Tin nhắn 1–2000 ký tự.")]
    public string Body { get; set; } = "";
}
