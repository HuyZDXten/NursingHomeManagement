using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Tests;

public sealed class WorkflowTests(WorkflowFixture fixture) : IClassFixture<WorkflowFixture>
{
    [Fact]
    public async Task EveryModuleAndTypedFormRendersForAdmin()
    {
        using var client = await fixture.LoginAsync(Roles.Admin);
        await using var database = fixture.Database();
        int residentId = await database.Residents.Select(resident => resident.Id).FirstAsync();
        int roomId = await database.Rooms.Select(room => room.Id).FirstAsync();
        int bedId = await database.Beds.Select(bed => bed.Id).FirstAsync();
        int taskId = await database.CareTasks.Select(task => task.Id).FirstAsync();
        var paths = new[]
        {
            "/Dashboard", "/Residents", "/Residents/Create", $"/Residents/Edit/{residentId}",
            $"/Residents/Details/{residentId}", $"/Residents/Contact?residentId={residentId}", $"/Residents/Link?residentId={residentId}",
            "/Rooms", "/Rooms/Create", $"/Rooms/Edit/{roomId}", $"/Rooms/Details/{roomId}",
            $"/Beds/Create?roomId={roomId}", $"/Beds/Edit/{bedId}", "/Beds/Assign", "/Beds/History",
            $"/Health?residentId={residentId}", $"/Health/Create?residentId={residentId}",
            "/CareTasks", "/CareTasks/Create", $"/CareTasks/Update/{taskId}", $"/CareTasks/History/{taskId}",
            "/Medication", "/Medication/Today", "/Medication/Create", $"/Medication/History?residentId={residentId}",
            "/Billing", "/Billing/Packages", "/Billing/Package", "/Billing/Subscriptions", "/Billing/Subscribe", "/Billing/Create",
            "/Users", "/Users/Create", "/Account/ChangePassword"
        };
        foreach (string path in paths)
        {
            var response = await client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }
        string form = await client.GetStringAsync("/Residents/Create");
        Assert.Contains("name=\"FullName\"", form);
        Assert.Contains("name=\"DateOfBirth\"", form);
        string assignment = await client.GetStringAsync("/Beds/Assign");
        Assert.Contains("DEMO-101", assignment);
        Assert.Contains("DEMO-001", assignment);
    }

    [Fact]
    public async Task ResidentHttpFormValidatesAndPersistsToSql()
    {
        using var client = await fixture.LoginAsync(Roles.Admin);
        string code = "HTTP-" + Guid.NewGuid().ToString("N")[..12];
        var values = new Dictionary<string, string>
        {
            ["Code"] = code,
            ["FullName"] = "Cư dân kiểm thử HTTP",
            ["DateOfBirth"] = "1950-01-01",
            ["Gender"] = "Nữ"
        };
        var response = await WorkflowFixture.PostAsync(client, "/Residents/Create", "/Residents/Create", values);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var database = fixture.Database();
        Assert.True(await database.Residents.AnyAsync(resident => resident.Code == code));
        values["Code"] = "";
        response = await WorkflowFixture.PostAsync(client, "/Residents/Create", "/Residents/Create", values);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BedTransferIsAtomicAndRejectsMaintenance()
    {
        using var scope = fixture.Scope(Roles.Admin);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<RoomService>();
        var resident = NewResident();
        var room = new Room { Number = Guid.NewGuid().ToString("N")[..15] };
        var first = new Bed { Room = room, Number = "1" };
        var second = new Bed { Room = room, Number = "2" };
        var maintenance = new Bed { Room = room, Number = "3", Status = BedStatus.Maintenance };
        database.AddRange(resident, first, second, maintenance);
        await database.SaveChangesAsync();
        await service.AssignAsync(new BedAssignmentForm { ResidentId = resident.Id, BedId = first.Id });
        await service.AssignAsync(new BedAssignmentForm { ResidentId = resident.Id, BedId = second.Id });
        await Assert.ThrowsAsync<BusinessException>(() => service.AssignAsync(new BedAssignmentForm { ResidentId = resident.Id, BedId = maintenance.Id }));
        Assert.Equal(BedStatus.Available, first.Status);
        Assert.Equal(BedStatus.Occupied, second.Status);
        Assert.Equal(1, await database.BedAssignments.CountAsync(item => item.ResidentId == resident.Id && item.EndedAt == null));
        Assert.Equal(2, await database.BedAssignments.CountAsync(item => item.ResidentId == resident.Id));
    }

    [Fact]
    public async Task ConcurrentBedRequestsAllowOnlyOneResident()
    {
        int bedId;
        int firstId;
        int secondId;
        await using (var database = fixture.Database())
        {
            var bed = new Bed { Number = "1", Room = new Room { Number = Guid.NewGuid().ToString("N")[..15] } };
            var first = NewResident();
            var second = NewResident();
            database.AddRange(bed, first, second);
            await database.SaveChangesAsync();
            bedId = bed.Id;
            firstId = first.Id;
            secondId = second.Id;
        }
        async Task<bool> Assign(int residentId)
        {
            using var scope = fixture.Scope(Roles.Admin);
            try
            {
                await scope.ServiceProvider.GetRequiredService<RoomService>().AssignAsync(new BedAssignmentForm { ResidentId = residentId, BedId = bedId });
                return true;
            }
            catch (Exception exception) when (exception is BusinessException || DatabaseConflict.IsExpected(exception))
            {
                return false;
            }
        }
        var results = await Task.WhenAll(Task.Run(() => Assign(firstId)), Task.Run(() => Assign(secondId)));
        Assert.Equal(1, results.Count(result => result));
        await using var check = fixture.Database();
        Assert.Equal(1, await check.BedAssignments.CountAsync(item => item.BedId == bedId && item.EndedAt == null));
    }

    [Fact]
    public async Task CareCompletionRequiresResultAndPreservesHistory()
    {
        using var scope = fixture.Scope(Roles.Admin);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CareTaskService>();
        var resident = NewResident();
        var caregiverId = await database.Users.Where(user => user.Email == "caregiver@demo.nursinghome.test").Select(user => user.Id).SingleAsync();
        var task = new CareTask { Resident = resident, AssignedToId = caregiverId, Title = "Test", DueAt = DateTimeOffset.UtcNow };
        database.Add(task);
        await database.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(task.Id, new CareUpdateForm { Status = CareTaskStatus.Completed, Result = "", RowVersion = Convert.ToBase64String(task.RowVersion) }));
        await service.UpdateAsync(task.Id, new CareUpdateForm { Status = CareTaskStatus.Completed, Result = "Đã thực hiện", RowVersion = Convert.ToBase64String(task.RowVersion) });
        Assert.Equal(CareTaskStatus.Completed, task.Status);
        Assert.Equal(1, await database.CareRecords.CountAsync(record => record.CareTaskId == task.Id));
        await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(task.Id, new CareUpdateForm { Status = CareTaskStatus.InProgress, Result = "Sửa lại", RowVersion = Convert.ToBase64String(task.RowVersion) }));
    }

    [Fact]
    public async Task MedicationRejectsFutureDuplicateAndMissingRefusalReason()
    {
        using var scope = fixture.Scope(Roles.Admin);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<MedicationService>();
        var clock = new FacilityClock();
        string adminId = scope.ServiceProvider.GetRequiredService<CurrentUser>().Id;
        var schedule = new MedicationSchedule
        {
            Resident = NewResident(),
            MedicationName = "Test",
            Dose = "Test",
            Route = "Test",
            ApprovalReference = "TEST",
            EnteredById = adminId,
            StartDate = clock.Today.AddDays(-1),
            ScheduledTime = TimeOnly.MinValue
        };
        database.Add(schedule);
        await database.SaveChangesAsync();
        var form = new MedicationLogForm { ScheduleId = schedule.Id, Date = clock.Today, Status = MedicationStatus.Refused };
        await Assert.ThrowsAsync<BusinessException>(() => service.RecordAsync(form));
        form.Notes = "Từ chối";
        form.Date = clock.Today.AddDays(1);
        await Assert.ThrowsAsync<BusinessException>(() => service.RecordAsync(form));
        form.Date = clock.Today;
        await service.RecordAsync(form);
        await Assert.ThrowsAsync<BusinessException>(() => service.RecordAsync(form));
        Assert.Equal(1, await database.MedicationLogs.CountAsync(log => log.MedicationScheduleId == schedule.Id));
    }

    [Fact]
    public async Task BillingSnapshotsPricesAndHandlesPartialPayments()
    {
        using var scope = fixture.Scope(Roles.Admin);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<BillingService>();
        var resident = NewResident();
        resident.AdmittedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, FacilityClock.Offset);
        var package = new ServicePackage { Name = "Test billing", MonthlyPrice = 3000000 };
        database.Add(new ResidentSubscription { Resident = resident, ServicePackage = package, StartDate = new DateOnly(2026, 1, 1), AgreedMonthlyPrice = 3000000 });
        await database.SaveChangesAsync();
        int id = await service.CreateAsync(new InvoiceForm { ResidentId = resident.Id, BillingPeriod = new DateOnly(2026, 11, 1), DueDate = new DateOnly(2026, 11, 30) });
        await service.AddItemAsync(id, new InvoiceItemForm { Description = "Phí bổ sung", Quantity = 2, UnitPrice = 100000 });
        await service.IssueAsync(id);
        await service.SavePackageAsync(package.Id, new PackageForm { Name = package.Name, MonthlyPrice = 9000000, IsActive = true });
        var invoice = await service.GetAsync(id);
        Assert.Equal(3200000, BillingService.Total(invoice));
        await Assert.ThrowsAsync<BusinessException>(() => service.AddItemAsync(id, new InvoiceItemForm { Description = "Không được thêm", Quantity = 1, UnitPrice = 1 }));
        await service.PayAsync(id, new PaymentForm { Amount = 1000000, Reference = Guid.NewGuid().ToString() });
        Assert.Equal(InvoiceStatus.PartiallyPaid, (await service.GetAsync(id)).Status);
        await Assert.ThrowsAsync<BusinessException>(() => service.PayAsync(id, new PaymentForm { Amount = 3000000, Reference = Guid.NewGuid().ToString() }));
        await service.PayAsync(id, new PaymentForm { Amount = 2200000, Reference = Guid.NewGuid().ToString() });
        Assert.Equal(InvoiceStatus.Paid, (await service.GetAsync(id)).Status);
        await Assert.ThrowsAsync<BusinessException>(() => service.CancelAsync(id));
    }

    [Fact]
    public async Task FamilyCannotReadOtherResidentsOrUnsharedRecords()
    {
        using var scope = fixture.Scope(Roles.Family);
        var service = scope.ServiceProvider.GetRequiredService<FamilyService>();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var linked = await database.Residents.SingleAsync(resident => resident.Code == "DEMO-001");
        var other = NewResident();
        database.Add(other);
        await database.SaveChangesAsync();
        var result = await service.DetailsAsync(linked.Id, 1);
        Assert.Empty(result.Health);
        Assert.Empty(result.Invoices);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DetailsAsync(other.Id, 1));
        using var client = await fixture.LoginAsync(Roles.Family);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/Family/Details/{other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Family/Details/{linked.Id}")).StatusCode);
    }

    [Fact]
    public async Task CaregiverCannotCreateScheduleOrReadUnassignedHealth()
    {
        using var scope = fixture.Scope(Roles.Caregiver);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resident = NewResident();
        database.Add(resident);
        await database.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<MedicationService>().CreateAsync(new MedicationForm()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<HealthService>().ListAsync(resident.Id, null, 1));
        using var client = await fixture.LoginAsync(Roles.Caregiver);
        var response = await client.GetAsync("/Medication/Create");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task DeactivatedAccountLosesExistingCookie()
    {
        using var adminScope = fixture.Scope(Roles.Admin);
        var accounts = adminScope.ServiceProvider.GetRequiredService<UserAccountService>();
        string email = Guid.NewGuid().ToString("N") + "@test.invalid";
        await accounts.CreateAsync(new UserForm { FullName = "Temporary", Email = email, Password = fixture.Password, Role = Roles.Family });
        using var client = fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var login = await WorkflowFixture.PostAsync(client, "/Account/Login", "/Account/Login", new Dictionary<string, string> { ["Email"] = email, ["Password"] = fixture.Password });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Family")).StatusCode);
        using var newAdminScope = fixture.Scope(Roles.Admin);
        var database = newAdminScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = await database.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        await newAdminScope.ServiceProvider.GetRequiredService<UserAccountService>().EditAsync(id, new UserEditForm { FullName = "Temporary", Role = Roles.Family, IsActive = false });
        var rejected = await client.GetAsync("/Family");
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Contains("Login", rejected.Headers.Location!.ToString());
    }


    [Fact]
    public async Task ConcurrentPaymentsCannotOverpayInvoice()
    {
        int invoiceId;
        using (var scope = fixture.Scope(Roles.Admin))
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoice = new Invoice
            {
                Resident = NewResident(),
                Number = Guid.NewGuid().ToString(),
                BillingPeriod = new DateOnly(2026, 11, 1),
                DueDate = new DateOnly(2026, 11, 30),
                Status = InvoiceStatus.Unpaid,
                IssuedAt = DateTimeOffset.UtcNow,
                Items = [new InvoiceItem { Description = "Test", Quantity = 1, UnitPrice = 100 }]
            };
            database.Add(invoice);
            await database.SaveChangesAsync();
            invoiceId = invoice.Id;
        }
        async Task<bool> Pay()
        {
            using var scope = fixture.Scope(Roles.Admin);
            try
            {
                await scope.ServiceProvider.GetRequiredService<BillingService>().PayAsync(invoiceId,
                    new PaymentForm { Amount = 80, Reference = Guid.NewGuid().ToString() });
                return true;
            }
            catch (Exception exception) when (exception is BusinessException || DatabaseConflict.IsExpected(exception))
            {
                return false;
            }
        }
        var results = await Task.WhenAll(Task.Run(Pay), Task.Run(Pay));
        Assert.Equal(1, results.Count(success => success));
        await using var check = fixture.Database();
        Assert.Equal(80, await check.Payments.Where(payment => payment.InvoiceId == invoiceId).SumAsync(payment => payment.Amount));
    }

    [Fact]
    public async Task FamilySharingFiltersIndividualHealthRecords()
    {
        int residentId;
        using (var scope = fixture.Scope(Roles.Admin))
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var resident = NewResident();
            database.Add(resident);
            await database.SaveChangesAsync();
            residentId = resident.Id;
            var familyId = await database.Users.Where(user => user.Email == "family@demo.nursinghome.test")
                .Select(user => user.Id).SingleAsync();
            await scope.ServiceProvider.GetRequiredService<ResidentService>().LinkAsync(new FamilyLinkForm
            {
                ResidentId = residentId,
                FamilyUserId = familyId,
                CanViewHealth = true
            });
            var health = scope.ServiceProvider.GetRequiredService<HealthService>();
            await health.RecordAsync(new HealthForm { ResidentId = residentId, ShareWithFamily = true, Notes = "Shared" });
            await health.RecordAsync(new HealthForm { ResidentId = residentId, ShareWithFamily = false, Notes = "Private" });
        }
        using var familyScope = fixture.Scope(Roles.Family);
        var details = await familyScope.ServiceProvider.GetRequiredService<FamilyService>().DetailsAsync(residentId, 1);
        Assert.Single(details.Health);
        Assert.Equal("Shared", details.Health[0].Notes);
        Assert.Empty(details.Invoices);
    }

    [Fact]
    public async Task DischargeReleasesBedAndStopsOutstandingCare()
    {
        using var scope = fixture.Scope(Roles.Admin);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resident = NewResident();
        var bed = new Bed { Number = "A", Room = new Room { Number = Guid.NewGuid().ToString("N")[..15] } };
        string caregiverId = await database.Users.Where(user => user.Email == "caregiver@demo.nursinghome.test")
            .Select(user => user.Id).SingleAsync();
        var task = new CareTask { Resident = resident, AssignedToId = caregiverId, Title = "Discharge test", DueAt = DateTimeOffset.UtcNow };
        database.AddRange(task, bed);
        await database.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<RoomService>().AssignAsync(new BedAssignmentForm { ResidentId = resident.Id, BedId = bed.Id });
        await scope.ServiceProvider.GetRequiredService<ResidentService>().DischargeAsync(resident.Id);
        Assert.Equal(ResidentStatus.Discharged, resident.Status);
        Assert.Equal(BedStatus.Available, bed.Status);
        Assert.Equal(CareTaskStatus.Cancelled, task.Status);
        Assert.True(await database.CareRecords.AnyAsync(record => record.CareTaskId == task.Id));
        Assert.False(await database.BedAssignments.AnyAsync(assignment => assignment.ResidentId == resident.Id && assignment.EndedAt == null));
    }

    [Fact]
    public async Task PasswordChangeWorksWithoutChangingOtherAccounts()
    {
        string email = Guid.NewGuid().ToString("N") + "@test.invalid";
        using (var scope = fixture.Scope(Roles.Admin))
        {
            await scope.ServiceProvider.GetRequiredService<UserAccountService>().CreateAsync(new UserForm
            {
                FullName = "Password test",
                Email = email,
                Password = fixture.Password,
                Role = Roles.Family
            });
        }
        using var client = fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var login = await WorkflowFixture.PostAsync(client, "/Account/Login", "/Account/Login",
            new Dictionary<string, string> { ["Email"] = email, ["Password"] = fixture.Password });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        string newPassword = "New!" + Guid.NewGuid().ToString("N") + "4a";
        var changed = await WorkflowFixture.PostAsync(client, "/Account/ChangePassword", "/Account/ChangePassword",
            new Dictionary<string, string>
            {
                ["CurrentPassword"] = fixture.Password,
                ["NewPassword"] = newPassword,
                ["ConfirmPassword"] = newPassword
            });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        using var scopeCheck = fixture.Factory.Services.CreateScope();
        var users = scopeCheck.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var account = await users.FindByEmailAsync(email);
        Assert.True(await users.CheckPasswordAsync(account!, newPassword));
        Assert.False(await users.CheckPasswordAsync(account!, fixture.Password));
    }

    private static Resident NewResident() => new()
    {
        Code = "TEST-" + Guid.NewGuid().ToString("N")[..15],
        FullName = "Cư dân kiểm thử",
        DateOfBirth = new DateOnly(1950, 1, 1),
        Gender = "Nữ"
    };
}
