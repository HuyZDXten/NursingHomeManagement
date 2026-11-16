using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.DTOs.Chat;

namespace NursingHome.Services.Helpers;

/// <summary>S3-V6 chat gia đình – nhân viên.</summary>
public static partial class OperationsMapper
{
    public static ConversationDto ToDto(this ConversationSummary summary) => new()
    {
        Id = summary.Id,
        ResidentId = summary.ResidentId,
        ResidentCode = summary.ResidentCode,
        ResidentName = summary.ResidentName,
        FamilyAccountId = summary.FamilyAccountId,
        FamilyName = summary.FamilyName,
        LastMessage = summary.LastMessage,
        LastMessageAt = summary.LastMessageAt,
        Unread = summary.Unread
    };

    /// <summary>Cần Include Sender (hoặc truyền <paramref name="sender"/> khi vừa tạo).</summary>
    public static ChatMessageDto ToDto(this ChatMessage message, Account? sender = null)
    {
        var from = sender ?? message.Sender;
        return new ChatMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = from.FullName,
            SenderRole = from.Role,
            FromFamily = message.FromFamily,
            Body = message.Body,
            SentAt = message.SentAt,
            ReadAt = message.ReadAt
        };
    }
}
