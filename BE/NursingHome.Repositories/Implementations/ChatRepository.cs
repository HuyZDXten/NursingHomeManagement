using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class ChatRepository : IChatRepository
{
    private static ChatDAO Dao => ChatDAO.Instance;

    public Task<Conversation> GetOrCreateConversationAsync(int residentId, int familyAccountId, DateTime now) =>
        Dao.GetOrCreateConversationAsync(residentId, familyAccountId, now);

    public Task<Conversation?> GetConversationAsync(int id) => Dao.GetConversationAsync(id);

    public Task<List<ConversationSummary>> ListConversationsAsync(int? familyAccountId, IReadOnlyCollection<int>? residentIds, bool viewerIsFamily) =>
        Dao.ListConversationsAsync(familyAccountId, residentIds, viewerIsFamily);

    public Task<List<ChatMessage>> ListMessagesAsync(int conversationId, long? beforeId, int take) => Dao.ListMessagesAsync(conversationId, beforeId, take);

    public Task<int> CountUnreadFromSideAsync(int conversationId, bool fromFamily) => Dao.CountUnreadFromSideAsync(conversationId, fromFamily);

    public Task<ChatMessage> AddMessageAsync(ChatMessage message) => Dao.AddMessageAsync(message);

    public Task<int> MarkReadAsync(int conversationId, bool readerIsFamily, DateTime now) => Dao.MarkReadAsync(conversationId, readerIsFamily, now);
}
