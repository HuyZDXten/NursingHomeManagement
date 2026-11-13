using NSubstitute;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;
using NursingHome.Services.Interfaces;

namespace NursingHome.Tests;

/// <summary>S3-V2: Caregiver xem cư dân khi có việc đang mở HOẶC đang là người phụ trách chính (repository giả).</summary>
public class ResidentAccessServiceTests
{
    private static readonly CurrentUser Caregiver = new(6, Roles.Caregiver, "caregiver@nursinghome.local", "Hoàng Văn Nam");

    private readonly ICareRepository care = Substitute.For<ICareRepository>();
    private readonly IStaffingRepository staffing = Substitute.For<IStaffingRepository>();
    private readonly ResidentAccessService service;

    public ResidentAccessServiceTests()
    {
        var residents = Substitute.For<IResidentRepository>();
        residents.GetStatusAsync(Arg.Any<int>()).Returns(ResidentStatus.Active);
        service = new ResidentAccessService(residents, care, staffing, new FacilityClock(TimeProvider.System));
    }

    [Fact]
    public async Task Caregiver_WithoutTaskOrAssignment_CannotRead()
    {
        Assert.False(await service.CanReadAsync(Caregiver, 3, ResidentSection.Basic));
        Assert.False(await service.CanReadAsync(Caregiver, 3, ResidentSection.Health));
    }

    [Fact]
    public async Task Caregiver_PrimaryAssignment_CanReadHealthButNotBilling()
    {
        staffing.IsPrimaryCaregiverAsync(3, 6, Arg.Any<DateOnly>()).Returns(true);

        Assert.True(await service.CanReadAsync(Caregiver, 3, ResidentSection.Health));
        Assert.True(await service.CanReadAsync(Caregiver, 3, ResidentSection.Care));
        Assert.False(await service.CanReadAsync(Caregiver, 3, ResidentSection.Billing));
    }

    [Fact]
    public async Task Caregiver_OpenTask_StillGrantsAccess()
    {
        care.HasOpenTaskAsync(1, 6).Returns(true);

        Assert.True(await service.CanReadAsync(Caregiver, 1, ResidentSection.Medication));
    }

    [Fact]
    public async Task VisibleResidentIds_UnionsTasksAndAssignments()
    {
        care.OpenTaskResidentIdsAsync(6).Returns(new List<int> { 1, 2 });
        staffing.PrimaryResidentIdsAsync(6, Arg.Any<DateOnly>()).Returns(new List<int> { 2, 3 });

        var visible = await service.VisibleResidentIdsAsync(Caregiver);

        Assert.NotNull(visible);
        Assert.Equal([1, 2, 3], visible.Order());
    }
}
