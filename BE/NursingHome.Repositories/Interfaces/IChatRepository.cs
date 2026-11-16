using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;

namespace NursingHome.Repositories.Interfaces;

/// <summary>MODULE VẬN HÀNH (Viên) — S3-V6 chat gia đình – nhân viên.</summary>
public interface IChatRepository
{
    Task<Conversation> GetOrCreateConversationAsync(int residentId, int familyAccountId, DateTime now);

    Task<Conversation?> GetConversationAsync(int id);

    Task<List<ConversationSummary>> ListConversationsAsync(int? familyAccountId, IReadOnlyCollection<int>? residentIds, bool viewerIsFamily);

    Task<List<ChatMessage>> ListMessagesAsync(int conversationId, long? beforeId, int take);

    Task<int> CountUnreadFromSideAsync(int conversationId, bool fromFamily);

    Task<ChatMessage> AddMessageAsync(ChatMessage message);

    Task<int> MarkReadAsync(int conversationId, bool readerIsFamily, DateTime now);
}
