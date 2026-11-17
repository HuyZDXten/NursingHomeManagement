using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DataAccessObjects;
using NursingHome.DTOs.Chat;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;
using NursingHome.Services.Interfaces;

namespace NursingHome.Tests;

/// <summary>S3-V6: quyền chat — người ngoài cuộc trò chuyện nhận 404, vai trò không dùng chat nhận 403 (repository giả).</summary>
public class ChatServiceTests
{
    private static readonly CurrentUser Family = new(8, Roles.Family, "family@nursinghome.local", "Nguyễn Thị Bích");
    private static readonly CurrentUser OtherFamily = new(9, Roles.Family, "family2@nursinghome.local", "Trần Quốc Bảo");
    private static readonly CurrentUser Caregiver = new(6, Roles.Caregiver, "caregiver@nursinghome.local", "Hoàng Văn Nam");
    private static readonly CurrentUser Accountant = new(3, Roles.Accountant, "accountant@nursinghome.local", "Phạm Minh Đức");

    private readonly IChatRepository chat = Substitute.For<IChatRepository>();
    private readonly IResidentRepository residents = Substitute.For<IResidentRepository>();
    private readonly IStaffingRepository staffing = Substitute.For<IStaffingRepository>();
    private readonly IChatPublisher publisher = Substitute.For<IChatPublisher>();
    private readonly IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
    private readonly IAccountRepository accounts = Substitute.For<IAccountRepository>();
    private readonly ChatService service;

    public ChatServiceTests()
    {
        chat.GetConversationAsync(1).Returns(new Conversation { Id = 1, ResidentId = 1, FamilyAccountId = 8, Resident = new Resident { Id = 1, Code = "NH-0001" } });
        residents.GetLinkAsync(1, 8).Returns(new ResidentFamilyLink { ResidentId = 1, FamilyAccountId = 8 });
        service = new ChatService(chat, residents, staffing, accounts, Substitute.For<INotificationService>(),
            publisher, cache, new FacilityClock(TimeProvider.System));
    }

    [Fact]
    public async Task Owner_CanReadMessages()
    {
        chat.ListMessagesAsync(1, null, 30).Returns([]);
        Assert.Empty(await service.MessagesAsync(Family, 1, null, 30));
    }

    [Fact]
    public async Task OtherFamily_GetsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => service.MessagesAsync(OtherFamily, 1, null, 30));
    }

    [Fact]
    public async Task Family_WhoseLinkWasRemoved_GetsNotFound()
    {
        residents.GetLinkAsync(1, 8).Returns((ResidentFamilyLink?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => service.MessagesAsync(Family, 1, null, 30));
    }

    [Fact]
    public async Task Caregiver_OnlyForPrimaryResidents()
    {
        staffing.PrimaryResidentIdsAsync(6, Arg.Any<DateOnly>()).Returns([2]);
        await Assert.ThrowsAsync<NotFoundException>(() => service.MessagesAsync(Caregiver, 1, null, 30));

        staffing.PrimaryResidentIdsAsync(6, Arg.Any<DateOnly>()).Returns([1, 2]);
        chat.ListMessagesAsync(1, null, 30).Returns([]);
        Assert.Empty(await service.MessagesAsync(Caregiver, 1, null, 30));
    }

    [Fact]
    public async Task Accountant_IsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAsync(Accountant));
    }

    // ---------------- Đã xem & đang nhập realtime ----------------

    [Fact]
    public async Task MarkRead_ByFamily_NotifiesTheStaffWhoSentTheMessages_AndTheReaderItself()
    {
        chat.MarkReadAsync(1, true, Arg.Any<DateTime>()).Returns(new ReadResult(2, [5, 6]));
        Assert.Equal(2, await service.MarkReadAsync(Family, 1));

        await publisher.Received(1).PublishReadAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { 5, 6, 8 })),
            Arg.Is<ChatReadDto>(read => read.ConversationId == 1 && read.ReaderId == 8 && read.ReaderIsFamily));
    }

    [Fact]
    public async Task MarkRead_ByStaff_NotifiesTheFamilyAccount()
    {
        staffing.PrimaryResidentIdsAsync(6, Arg.Any<DateOnly>()).Returns([1]);
        chat.MarkReadAsync(1, false, Arg.Any<DateTime>()).Returns(new ReadResult(1, [8]));
        await service.MarkReadAsync(Caregiver, 1);

        await publisher.Received(1).PublishReadAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { 6, 8 })),
            Arg.Is<ChatReadDto>(read => !read.ReaderIsFamily && read.ReaderId == 6));
    }

    [Fact]
    public async Task MarkRead_WithNothingNew_PublishesNothing()
    {
        chat.MarkReadAsync(1, true, Arg.Any<DateTime>()).Returns(new ReadResult(0, []));
        Assert.Equal(0, await service.MarkReadAsync(Family, 1));
        await publisher.DidNotReceiveWithAnyArgs().PublishReadAsync(default!, default!);
    }

    [Fact]
    public async Task MarkRead_ByOutsider_GetsNotFound_AndPublishesNothing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => service.MarkReadAsync(OtherFamily, 1));
        await publisher.DidNotReceiveWithAnyArgs().PublishReadAsync(default!, default!);
    }

    [Fact]
    public async Task Typing_ByStaff_GoesOnlyToTheFamilyAccount_NeverBackToTheSender()
    {
        staffing.PrimaryResidentIdsAsync(6, Arg.Any<DateOnly>()).Returns([1]);
        await service.TypingAsync(Caregiver, 1);

        await publisher.Received(1).PublishTypingAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 8 })),
            Arg.Is<ChatTypingDto>(typing => typing.ConversationId == 1 && typing.SenderId == 6 && !typing.FromFamily && typing.SenderName == "Hoàng Văn Nam"));
    }

    [Fact]
    public async Task Typing_ByFamily_GoesToThePrimaryStaff_AndFallsBackToReceptionists()
    {
        var yesterday = new FacilityClock(TimeProvider.System).Today.AddDays(-1);
        staffing.ListCareAssignmentsAsync(1, null, true).Returns([new ResidentCareAssignment { ResidentId = 1, AccountId = 6, StartDate = yesterday }]);
        staffing.ListAssignmentsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null).Returns([]);
        await service.TypingAsync(Family, 1);
        await publisher.Received(1).PublishTypingAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 6 })),
            Arg.Is<ChatTypingDto>(typing => typing.FromFamily && typing.SenderId == 8));

        // Không ai phụ trách và không ai có ca: báo cho lễ tân.
        publisher.ClearReceivedCalls();
        cache.Remove("chat-typing:8:1");
        staffing.ListCareAssignmentsAsync(1, null, true).Returns([]);
        accounts.ListActiveByRolesAsync(Arg.Any<IReadOnlyCollection<string>>()).Returns([new Account { Id = 2, Role = Roles.Receptionist }]);
        await service.TypingAsync(Family, 1);
        await publisher.Received(1).PublishTypingAsync(Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 2 })), Arg.Any<ChatTypingDto>());
    }

    [Fact]
    public async Task Typing_IsThrottledPerUserAndConversation()
    {
        staffing.PrimaryResidentIdsAsync(6, Arg.Any<DateOnly>()).Returns([1]);
        await service.TypingAsync(Caregiver, 1);
        await service.TypingAsync(Caregiver, 1);
        await service.TypingAsync(Caregiver, 1);
        await publisher.Received(1).PublishTypingAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ChatTypingDto>());

        Assert.True(ChatService.TypingThrottle >= TimeSpan.FromSeconds(1)); // đủ thưa để không làm ngập kết nối
    }

    [Fact]
    public async Task Typing_ByOutsider_GetsNotFound_AndPublishesNothing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => service.TypingAsync(OtherFamily, 1));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.TypingAsync(Accountant, 1));
        await publisher.DidNotReceiveWithAnyArgs().PublishTypingAsync(default!, default!);
    }

    [Fact]
    public void Preview_TruncatesLongBodies()
    {
        Assert.Equal("ngắn", ChatService.Preview("ngắn"));
        var preview = ChatService.Preview(new string('a', 150));
        Assert.Equal(100, preview.Length);
        Assert.EndsWith("...", preview);
    }
}
