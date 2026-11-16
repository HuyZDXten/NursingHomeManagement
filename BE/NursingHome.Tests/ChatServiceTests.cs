using NSubstitute;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
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
    private readonly ChatService service;

    public ChatServiceTests()
    {
        chat.GetConversationAsync(1).Returns(new Conversation { Id = 1, ResidentId = 1, FamilyAccountId = 8, Resident = new Resident { Id = 1, Code = "NH-0001" } });
        residents.GetLinkAsync(1, 8).Returns(new ResidentFamilyLink { ResidentId = 1, FamilyAccountId = 8 });
        service = new ChatService(chat, residents, staffing, Substitute.For<IAccountRepository>(), Substitute.For<INotificationService>(),
            Substitute.For<IChatPublisher>(), new FacilityClock(TimeProvider.System));
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

    [Fact]
    public void Preview_TruncatesLongBodies()
    {
        Assert.Equal("ngắn", ChatService.Preview("ngắn"));
        var preview = ChatService.Preview(new string('a', 150));
        Assert.Equal(100, preview.Length);
        Assert.EndsWith("...", preview);
    }
}
