using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;

namespace NursingHome.DataAccessObjects;

/// <summary>Dòng danh sách cuộc trò chuyện: tin cuối + số tin bên kia gửi mà người xem chưa đọc.</summary>
public sealed record ConversationSummary(
    int Id,
    int ResidentId,
    string ResidentCode,
    string ResidentName,
    int FamilyAccountId,
    string FamilyName,
    string? LastMessage,
    DateTime? LastMessageAt,
    int Unread);

/// <summary>MODULE VẬN HÀNH (Viên) — S3-V6: chat gia đình – nhân viên (UC-106).</summary>
public sealed class ChatDAO : GenericDAO<Conversation>
{
    public static ChatDAO Instance { get; } = new();

    private ChatDAO()
    {
    }

    /// <summary>Mở hoặc lấy lại cuộc trò chuyện; 2 request cùng tạo → UQ_Conversations chặn một bên, bên đó đọc lại.</summary>
    public async Task<Conversation> GetOrCreateConversationAsync(int residentId, int familyAccountId, DateTime now)
    {
        var existing = await FindAsync(residentId, familyAccountId);
        if (existing is not null)
        {
            return existing;
        }

        try
        {
            await using var context = CreateContext();
            context.Conversations.Add(new Conversation { ResidentId = residentId, FamilyAccountId = familyAccountId, CreatedAt = now });
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Request khác vừa tạo cùng cặp — dùng lại bản đó.
        }

        return await FindAsync(residentId, familyAccountId) ?? throw new InvalidOperationException("Không tạo được cuộc trò chuyện.");
    }

    public async Task<Conversation?> GetConversationAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Conversations.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.FamilyAccount)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    /// <param name="familyAccountId">Có giá trị = cuộc trò chuyện của gia đình này (kể cả chưa có tin).</param>
    /// <param name="residentIds">Nhân viên: NULL = mọi cư dân; chỉ lấy cuộc trò chuyện đã có tin.</param>
    /// <param name="viewerIsFamily">Đếm chưa đọc = tin của bên kia chưa có ReadAt.</param>
    public async Task<List<ConversationSummary>> ListConversationsAsync(int? familyAccountId, IReadOnlyCollection<int>? residentIds, bool viewerIsFamily)
    {
        var ids = residentIds?.ToList();
        await using var context = CreateContext();
        return await context.Conversations.AsNoTracking()
            .Where(item => (familyAccountId == null ? item.LastMessageAt != null : item.FamilyAccountId == familyAccountId)
                && (ids == null || ids.Contains(item.ResidentId)))
            .OrderByDescending(item => item.LastMessageAt ?? item.CreatedAt)
            .Select(item => new ConversationSummary(
                item.Id,
                item.ResidentId,
                item.Resident.Code,
                item.Resident.FullName,
                item.FamilyAccountId,
                item.FamilyAccount.FullName,
                item.Messages.OrderByDescending(message => message.Id).Select(message => message.Body).FirstOrDefault(),
                item.LastMessageAt,
                item.Messages.Count(message => message.FromFamily != viewerIsFamily && message.ReadAt == null)))
            .Take(200)
            .ToListAsync();
    }

    /// <summary>Tin mới nhất trước <paramref name="beforeId"/> (cuộn lên tải tin cũ), trả theo thứ tự thời gian.</summary>
    public async Task<List<ChatMessage>> ListMessagesAsync(int conversationId, long? beforeId, int take)
    {
        await using var context = CreateContext();
        var page = await context.ChatMessages.AsNoTracking()
            .Include(item => item.Sender)
            .Where(item => item.ConversationId == conversationId && (beforeId == null || item.Id < beforeId))
            .OrderByDescending(item => item.Id)
            .Take(take)
            .ToListAsync();
        page.Reverse();
        return page;
    }

    /// <summary>Số tin của cùng phía người gửi còn chưa được bên kia đọc (0 = tin này mở đầu một lượt mới → báo chuông).</summary>
    public async Task<int> CountUnreadFromSideAsync(int conversationId, bool fromFamily)
    {
        await using var context = CreateContext();
        return await context.ChatMessages.CountAsync(item =>
            item.ConversationId == conversationId && item.FromFamily == fromFamily && item.ReadAt == null);
    }

    /// <summary>Thêm tin và cập nhật LastMessageAt trong cùng một SaveChanges.</summary>
    public async Task<ChatMessage> AddMessageAsync(ChatMessage message)
    {
        await using var context = CreateContext();
        var conversation = await context.Conversations.FindAsync(message.ConversationId)
            ?? throw new InvalidOperationException("Không tìm thấy cuộc trò chuyện.");
        conversation.LastMessageAt = message.SentAt;
        context.ChatMessages.Add(message);
        await context.SaveChangesAsync();
        return message;
    }

    /// <summary>Đánh dấu đã đọc mọi tin của bên kia; trả số tin vừa đánh dấu.</summary>
    public async Task<int> MarkReadAsync(int conversationId, bool readerIsFamily, DateTime now)
    {
        await using var context = CreateContext();
        return await context.ChatMessages
            .Where(item => item.ConversationId == conversationId && item.FromFamily != readerIsFamily && item.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, now));
    }

    private static async Task<Conversation?> FindAsync(int residentId, int familyAccountId)
    {
        await using var context = CreateContext();
        return await context.Conversations.AsNoTracking()
            .Include(item => item.Resident)
            .Include(item => item.FamilyAccount)
            .FirstOrDefaultAsync(item => item.ResidentId == residentId && item.FamilyAccountId == familyAccountId);
    }
}
