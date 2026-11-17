using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DataAccessObjects;
using NursingHome.DTOs.Admin;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Tests;

/// <summary>BL-V5: nhật ký thao tác, cấu hình cơ sở, thông báo hàng loạt & kênh nhận, tin tức & album.</summary>
public class SystemAdminTests
{
    private static readonly CurrentUser Admin = new(1, Roles.Admin, "admin@nursinghome.local", "Quản trị viên");
    private static readonly FacilityClock Clock = new(TimeProvider.System);

    // ---------------- Cấu hình cơ sở (UC-129) ----------------

    [Theory]
    [InlineData("Facility:TourSlotCapacity", "5", "5")]
    [InlineData("Facility:TourSlotCapacity", " 12 ", "12")]
    [InlineData("Facility:VitalAlerts:TemperatureHigh", "38.5", "38.5")]
    [InlineData("Facility:VitalAlerts:TemperatureHigh", "38.50", "38.5")]
    [InlineData("Facility:Name", "  Viện An Khang  ", "Viện An Khang")]
    [InlineData("Facility:Hotline", "", "")]
    public void Settings_NormalizeAcceptsValidValues(string key, string raw, string expected) =>
        Assert.Equal(expected, SettingsCatalog.Normalize(SettingsCatalog.Find(key)!, raw));

    [Theory]
    [InlineData("Facility:TourSlotCapacity", "0")]
    [InlineData("Facility:TourSlotCapacity", "51")]
    [InlineData("Facility:TourSlotCapacity", "abc")]
    [InlineData("Facility:TourSlotCapacity", "3.5")]
    [InlineData("Facility:VitalAlerts:TemperatureHigh", "36")]
    [InlineData("Facility:VitalAlerts:TemperatureHigh", "38,5")] // dấu phẩy: không hiểu theo locale vi-VN
    [InlineData("Facility:VitalAlerts:SpO2Low", "99")]
    [InlineData("Facility:Name", "   ")]
    public void Settings_NormalizeRejectsInvalidValues(string key, string raw) =>
        Assert.Throws<BusinessRuleException>(() => SettingsCatalog.Normalize(SettingsCatalog.Find(key)!, raw));

    [Fact]
    public void Settings_TextTooLongIsRejected() =>
        Assert.Throws<BusinessRuleException>(() => SettingsCatalog.Normalize(SettingsCatalog.Find("Facility:Address")!, new string('a', SettingsCatalog.MaxTextLength + 1)));

    [Fact]
    public void Settings_OnlyWhitelistedKeysExist()
    {
        Assert.Null(SettingsCatalog.Find("Jwt:Key"));
        Assert.Null(SettingsCatalog.Find("Email:SmtpPassword"));
        Assert.Null(SettingsCatalog.Find("PayOs:ApiKey"));
        Assert.Equal(SettingsCatalog.All.Count, SettingsCatalog.All.Select(item => item.Key).Distinct().Count());
        Assert.Throws<BusinessRuleException>(() => SettingsCatalog.Read(new FacilityOptions(), "Jwt:Key"));
        Assert.Throws<BusinessRuleException>(() => SettingsCatalog.Write(new FacilityOptions(), "Jwt:Key", "x"));
    }

    [Fact]
    public void Settings_ReadWriteRoundTripForEveryKey()
    {
        var source = new FacilityOptions();
        var target = new FacilityOptions { Name = "Khác", TourSlotCapacity = 1, VisitSlotCapacity = 1 };
        foreach (var definition in SettingsCatalog.All)
        {
            SettingsCatalog.Write(target, definition.Key, SettingsCatalog.Normalize(definition, SettingsCatalog.Read(source, definition.Key)));
            Assert.Equal(SettingsCatalog.Read(source, definition.Key), SettingsCatalog.Read(target, definition.Key));
        }
    }

    [Fact]
    public void Settings_EmptyOptionalTextBecomesNull()
    {
        var options = new FacilityOptions { Hotline = "0236 000 0000" };
        SettingsCatalog.Write(options, "Facility:Hotline", "");
        Assert.Null(options.Hotline);
    }

    [Fact]
    public void Settings_ThresholdPairsMustBeOrdered()
    {
        var options = new FacilityOptions();
        SettingsCatalog.ValidateConsistency(options);
        options.VitalAlerts.SystolicLow = options.VitalAlerts.SystolicHigh;
        Assert.Throws<BusinessRuleException>(() => SettingsCatalog.ValidateConsistency(options));
        options.VitalAlerts.SystolicLow = 90;
        options.VitalAlerts.HeartRateLow = options.VitalAlerts.HeartRateHigh + 1;
        Assert.Throws<BusinessRuleException>(() => SettingsCatalog.ValidateConsistency(options));
    }

    [Fact]
    public void Settings_CloneIsIndependent()
    {
        var original = new FacilityOptions();
        var copy = SettingsCatalog.Clone(original);
        copy.VitalAlerts.SystolicHigh = 200;
        copy.Name = "Khác";
        Assert.Equal(160, original.VitalAlerts.SystolicHigh);
        Assert.Equal("Viện Dưỡng Lão", original.Name);
    }

    private static FacilitySettingsStore Store(params (string Key, string Value)[] appsettings) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(appsettings.Select(item => new KeyValuePair<string, string?>(item.Key, item.Value))).Build());

    [Fact]
    public void Store_OverridesWinAndResetRestoresBaseline()
    {
        var store = Store(("Facility:Name", "Viện A"), ("Facility:TourSlotCapacity", "5"));
        Assert.Equal(5, store.Value.TourSlotCapacity);

        store.Apply([new SystemSetting { Key = "Facility:TourSlotCapacity", Value = "8" }, new SystemSetting { Key = "Facility:Hotline", Value = "0236 1" }]);
        Assert.Equal(8, store.Value.TourSlotCapacity);
        Assert.Equal("0236 1", store.Value.Hotline);
        Assert.Equal("Viện A", store.Value.Name);
        Assert.Equal(5, store.Baseline.TourSlotCapacity); // appsettings không đổi

        store.Apply([]);
        Assert.Equal(5, store.Value.TourSlotCapacity);
        Assert.Null(store.Value.Hotline);
    }

    [Fact]
    public void Store_IgnoresUnknownKeysAndBrokenRows()
    {
        var store = Store(("Facility:TourSlotCapacity", "5"));
        store.Apply([
            new SystemSetting { Key = "Jwt:Key", Value = "x" },
            new SystemSetting { Key = "Facility:TourSlotCapacity", Value = "không phải số" },
            new SystemSetting { Key = "Facility:VisitSlotCapacity", Value = "6" }]);
        Assert.Equal(5, store.Value.TourSlotCapacity);
        Assert.Equal(6, store.Value.VisitSlotCapacity);
    }

    [Fact]
    public void Store_InconsistentOverridesFallBackToBaseline()
    {
        var store = Store();
        store.Apply([new SystemSetting { Key = "Facility:VitalAlerts:SystolicLow", Value = "120" }, new SystemSetting { Key = "Facility:VitalAlerts:SystolicHigh", Value = "120" }]);
        Assert.Equal(160, store.Value.VitalAlerts.SystolicHigh);
        Assert.Equal(90, store.Value.VitalAlerts.SystolicLow);
    }

    [Fact]
    public async Task SettingsService_SavesAllOrNothingAndReloadsStore()
    {
        var store = Store(("Facility:TourSlotCapacity", "5"));
        var system = Substitute.For<ISystemRepository>();
        var saved = new List<SystemSetting>();
        system.SaveSettingsAsync(Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<int?>(), Arg.Any<DateTime>())
            .Returns(call =>
            {
                saved.AddRange(call.Arg<IReadOnlyDictionary<string, string>>().Select(pair => new SystemSetting { Key = pair.Key, Value = pair.Value }));
                return Task.CompletedTask;
            });
        system.ListSettingsAsync().Returns(_ => Task.FromResult(saved.ToList()));
        var audit = Substitute.For<IAuditService>();
        var service = new SettingsService(system, store, Clock, audit);

        var bad = new SaveSettingsRequest { Values = [new() { Key = "Facility:Hotline", Value = "0236" }, new() { Key = "Facility:TourSlotCapacity", Value = "0" }] };
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(Admin, bad));
        await system.DidNotReceive().SaveSettingsAsync(Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<int?>(), Arg.Any<DateTime>());

        var good = new SaveSettingsRequest { Values = [new() { Key = "Facility:Hotline", Value = " 0236 777 " }, new() { Key = "Facility:TourSlotCapacity", Value = "9" }] };
        var result = await service.SaveAsync(Admin, good);
        Assert.Equal(9, store.Value.TourSlotCapacity);
        Assert.Equal("0236 777", store.Value.Hotline);
        Assert.Equal(2, result.Count(item => item.IsOverridden));
        await audit.Received(1).RecordAsync(Admin, "Settings.Update", "Settings", Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task SettingsService_SameKeyTwiceAndUnknownKeyAreRejected()
    {
        var service = new SettingsService(Substitute.For<ISystemRepository>(), Store(), Clock, Substitute.For<IAuditService>());
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(Admin, new SaveSettingsRequest { Values = [new() { Key = "Jwt:Key", Value = "x" }] }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(Admin, new SaveSettingsRequest
        {
            Values = [new() { Key = "Facility:Hotline", Value = "1" }, new() { Key = "Facility:Hotline", Value = "2" }]
        }));
    }

    // ---------------- Nhật ký thao tác (UC-13) ----------------

    [Fact]
    public async Task Audit_RecordTruncatesAndNeverThrows()
    {
        var audits = Substitute.For<IAuditRepository>();
        AuditLog? written = null;
        audits.AddAsync(Arg.Do<AuditLog>(log => written = log)).Returns(Task.CompletedTask);
        var service = new AuditService(audits, Clock, NullLogger<AuditService>.Instance);

        await service.RecordAsync(Admin, new string('a', 100), "Room", "5", new string('b', 500), new { Method = "POST" }, "::1");
        Assert.Equal(60, written!.Action.Length);
        Assert.Equal(400, written.Summary.Length);
        Assert.Equal(Admin.AccountId, written.AccountId);
        Assert.Equal("Quản trị viên", written.ActorName);
        Assert.Contains("POST", written.DataJson);

        audits.AddAsync(Arg.Any<AuditLog>()).Returns<Task>(_ => throw new InvalidOperationException("DB down"));
        await service.RecordAsync(Admin, "Room.Create", "Room", "1", "x"); // không ném: nhật ký lỗi không làm hỏng thao tác chính
    }

    [Fact]
    public async Task Audit_SystemActorHasNoAccount()
    {
        var audits = Substitute.For<IAuditRepository>();
        AuditLog? written = null;
        audits.AddAsync(Arg.Do<AuditLog>(log => written = log)).Returns(Task.CompletedTask);
        await new AuditService(audits, Clock, NullLogger<AuditService>.Instance).RecordAsync(null, "Job.Run", "Job", null, "Job nền chạy");
        Assert.Null(written!.AccountId);
        Assert.Null(written.ActorName);
    }

    [Fact]
    public async Task Audit_ListClampsPageSizeAndConvertsFacilityDates()
    {
        var audits = Substitute.For<IAuditRepository>();
        DateTime? from = null, to = null;
        int skip = -1, take = -1;
        audits.SearchAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(call =>
            {
                from = call.ArgAt<DateTime?>(0);
                to = call.ArgAt<DateTime?>(1);
                skip = call.ArgAt<int>(6);
                take = call.ArgAt<int>(7);
                return Task.FromResult((new List<AuditLog> { new() { Action = "A", EntityType = "E", Summary = "S" } }, 1));
            });
        var service = new AuditService(audits, Clock, NullLogger<AuditService>.Instance);

        var page = await service.ListAsync(new AuditQuery { From = new DateOnly(2026, 11, 17), To = new DateOnly(2026, 11, 17), Page = 3, PageSize = 5000 });
        Assert.Equal(100, page.PageSize);
        Assert.Equal(200, skip);
        Assert.Equal(100, take);
        Assert.Equal(new DateTime(2026, 11, 16, 17, 0, 0, DateTimeKind.Utc), from); // 00:00 giờ VN = 17:00 UTC hôm trước
        Assert.Equal(new DateTime(2026, 11, 17, 17, 0, 0, DateTimeKind.Utc), to);   // hết ngày To (loại trừ)
        Assert.Equal("(hệ thống)", page.Items[0].ActorName);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ListAsync(new AuditQuery { From = new DateOnly(2026, 11, 18), To = new DateOnly(2026, 11, 17) }));
        Assert.Equal(1, (await service.ListAsync(new AuditQuery { Page = -4, PageSize = 0 })).Page);
    }

    // ---------------- Thông báo hàng loạt (UC-119) ----------------

    [Fact]
    public void Announcement_AudiencesMapToRoles()
    {
        Assert.Equal(Roles.All, AnnouncementRules.RolesFor("All", null));
        Assert.Equal([Roles.Family], AnnouncementRules.RolesFor("Family", null));
        Assert.Equal(Roles.Staff, AnnouncementRules.RolesFor("Staff", null));
        Assert.Equal([Roles.Nurse], AnnouncementRules.RolesFor("Role", Roles.Nurse));
        Assert.DoesNotContain(Roles.Family, AnnouncementRules.RolesFor("Staff", null));
    }

    [Theory]
    [InlineData("Role", null)]
    [InlineData("Role", "")]
    [InlineData("Role", "Hacker")]
    [InlineData("Staff", "Nurse")]
    [InlineData("Everyone", null)]
    [InlineData("all", null)]
    public void Announcement_InvalidAudienceOrRoleIsRejected(string audience, string? role) =>
        Assert.Throws<BusinessRuleException>(() => AnnouncementRules.RolesFor(audience, role));

    [Fact]
    public void Announcement_NotificationBodyIsCappedAtColumnSize()
    {
        Assert.Equal("ngắn", AnnouncementRules.NotificationBody("ngắn"));
        Assert.Equal(1000, AnnouncementRules.NotificationBody(new string('x', 2000)).Length);
        Assert.Equal(1000, AnnouncementRules.NotificationBody(new string('x', 1000)).Length);
    }

    private static (AnnouncementService Service, ICommsRepository Comms, INotificationService Notifications, IEmailService Email) NewAnnouncementService(IReadOnlyList<Account> recipients, bool smtp)
    {
        var comms = Substitute.For<ICommsRepository>();
        comms.AddAnnouncementAsync(Arg.Any<Announcement>()).Returns(call => { var item = call.Arg<Announcement>(); item.Id = 7; return item; });
        var accounts = Substitute.For<IAccountRepository>();
        accounts.ListActiveByRolesAsync(Arg.Any<IReadOnlyCollection<string>>()).Returns(recipients.ToList());
        var notifications = Substitute.For<INotificationService>();
        var email = Substitute.For<IEmailService>();
        email.IsConfigured.Returns(smtp);
        var service = new AnnouncementService(comms, accounts, notifications, email, Options.Create(new FacilityOptions { Name = "Viện A" }), Clock, NullLogger<AnnouncementService>.Instance);
        return (service, comms, notifications, email);
    }

    private static List<Account> Recipients() =>
    [
        new() { Id = 10, Email = "A@Example.com", Role = Roles.Family },
        new() { Id = 11, Email = "b@example.com", Role = Roles.Family },
        new() { Id = 12, Email = "c@example.com", Role = Roles.Family }
    ];

    [Fact]
    public async Task Announcement_AlwaysNotifiesInApp_EmailOnlyWhenAskedAndConfigured()
    {
        var (service, _, notifications, email) = NewAnnouncementService(Recipients(), smtp: true);
        var request = new CreateAnnouncementRequest { Title = " Tiêu đề ", Body = "Nội dung", Audience = "Family", SendEmail = false };

        var result = await service.SendAsync(Admin, request);
        Assert.Equal(3, result.RecipientCount);
        Assert.Equal("Tiêu đề", result.Title);
        Assert.Equal(0, result.EmailSentCount);
        await notifications.Received(1).NotifyAsync(Arg.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 10, 11, 12 })), NotificationType.System, "Tiêu đề", "Nội dung", null);
        await email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!);

        var (noSmtp, _, _, noSmtpEmail) = NewAnnouncementService(Recipients(), smtp: false);
        request.SendEmail = true;
        Assert.Equal(0, (await noSmtp.SendAsync(Admin, request)).EmailSentCount);
        await noSmtpEmail.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Announcement_EmailSkipsPeopleWhoOptedOut_AndSurvivesSendFailures()
    {
        var (service, comms, _, email) = NewAnnouncementService(Recipients(), smtp: true);
        comms.ListOptedOutEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), EmailChannel.Announcements).Returns(new HashSet<string> { "a@example.com" });
        email.SendAsync("c@example.com", Arg.Any<string>(), Arg.Any<string>()).Returns<Task>(_ => throw new InvalidOperationException("SMTP lỗi"));

        var result = await service.SendAsync(Admin, new CreateAnnouncementRequest { Title = "T", Body = "B", Audience = "Family", SendEmail = true });
        await email.DidNotReceive().SendAsync("A@Example.com", Arg.Any<string>(), Arg.Any<string>()); // đã tắt kênh (so khớp không phân biệt hoa thường)
        await email.Received(1).SendAsync("b@example.com", Arg.Any<string>(), Arg.Any<string>());
        Assert.Equal(1, result.EmailSentCount); // c lỗi không kéo cả đợt gửi hỏng
        Assert.Equal(3, result.RecipientCount);
        await comms.Received(1).SetAnnouncementResultAsync(7, 3, 1);
    }

    [Fact]
    public async Task Announcement_EmptyAudienceAndBlankTextAreRejected()
    {
        var (empty, _, notifications, _) = NewAnnouncementService([], smtp: false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => empty.SendAsync(Admin, new CreateAnnouncementRequest { Title = "T", Body = "B", Audience = "Family" }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => empty.SendAsync(Admin, new CreateAnnouncementRequest { Title = "  ", Body = "B", Audience = "All" }));
        await notifications.DidNotReceiveWithAnyArgs().NotifyAsync(default!, default, default!, default, default);
    }

    [Fact]
    public async Task InvoiceMailer_RespectsEmailPreferences()
    {
        var residents = Substitute.For<IResidentRepository>();
        residents.FamilyEmailsAsync(5, true).Returns(["Nguoi.A@example.com", "nguoi.b@example.com"]);
        var comms = Substitute.For<ICommsRepository>();
        comms.ListOptedOutEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), EmailChannel.Invoices).Returns(new HashSet<string> { "nguoi.b@example.com" });
        comms.ListOptedOutEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), EmailChannel.Reminders).Returns(new HashSet<string> { "nguoi.a@example.com" });
        var email = Substitute.For<IEmailService>();
        email.IsConfigured.Returns(true);
        var mailer = new InvoiceMailer(residents, comms, email, Options.Create(new EmailOptions()), Options.Create(new FacilityOptions()), NullLogger<InvoiceMailer>.Instance);
        var invoice = new InvoiceListItemDto { Id = 1, Number = "HD-1", ResidentId = 5, ResidentName = "Cư dân", BillingPeriod = new DateOnly(2026, 11, 1), DueDate = new DateOnly(2026, 11, 20), Total = 100, Remaining = 100 };

        Assert.Equal(1, await mailer.SendIssuedAsync(invoice));
        await email.Received(1).SendAsync("Nguoi.A@example.com", Arg.Any<string>(), Arg.Any<string>());
        await email.DidNotReceive().SendAsync("nguoi.b@example.com", Arg.Any<string>(), Arg.Any<string>());

        email.ClearReceivedCalls();
        Assert.Equal(1, await mailer.SendReminderAsync(invoice, ReminderStage.Overdue));
        await email.Received(1).SendAsync("nguoi.b@example.com", Arg.Any<string>(), Arg.Any<string>());
        await email.DidNotReceive().SendAsync("Nguoi.A@example.com", Arg.Any<string>(), Arg.Any<string>());
    }

    // ---------------- Tin tức & album (UC-19, UC-115) ----------------

    [Theory]
    [InlineData("Chương trình khám sức khỏe định kỳ", "chuong-trinh-kham-suc-khoe-dinh-ky")]
    [InlineData("  Đà Nẵng — Ngày hội 2026!  ", "da-nang-ngay-hoi-2026")]
    [InlineData("Album: Tết Trung thu / 2026", "album-tet-trung-thu-2026")]
    [InlineData("ĐỊA CHỈ MỚI", "dia-chi-moi")]
    [InlineData("!!!", "bai-viet")]
    [InlineData("", "bai-viet")]
    public void Post_SlugifyRemovesDiacritics(string title, string expected) => Assert.Equal(expected, PostRules.Slugify(title));

    [Fact]
    public void Post_SlugifyIsBoundedAndNeverEndsWithDash()
    {
        string slug = PostRules.Slugify(string.Join(' ', Enumerable.Repeat("từ", 300)));
        Assert.True(slug.Length <= PostRules.MaxSlugLength);
        Assert.False(slug.EndsWith('-'));
        Assert.False(slug.StartsWith('-'));
    }

    [Theory]
    [InlineData("/uploads/images/abc.jpg", true)]
    [InlineData("/uploads/residents/1.webp", true)]
    [InlineData("https://res.cloudinary.com/demo/image/upload/v1/a.jpg", true)]
    [InlineData("https://RES.CLOUDINARY.COM/demo/a.jpg", true)]
    [InlineData("http://res.cloudinary.com/demo/a.jpg", false)]
    [InlineData("https://res.cloudinary.com.evil.example/a.jpg", false)]
    [InlineData("https://evil.example/a.jpg", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:image/png;base64,AAAA", false)]
    [InlineData("//evil.example/a.jpg", false)]
    [InlineData("/uploads/../appsettings.json", false)]
    [InlineData("/uploads/..\\appsettings.json", false)]
    [InlineData("/other/a.jpg", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Post_OnlyTrustedImageUrlsAreAccepted(string? url, bool expected) => Assert.Equal(expected, PostRules.IsTrustedImageUrl(url));

    [Fact]
    public void Post_AlbumNeedsConsentAndImagesToStayPublished()
    {
        PostRules.ValidatePublished(PostKind.News, consentConfirmed: false, imageCount: 0); // tin tức không cần
        PostRules.ValidatePublished(PostKind.Album, true, 3);
        Assert.Throws<BusinessRuleException>(() => PostRules.ValidatePublished(PostKind.Album, false, 3));
        Assert.Throws<BusinessRuleException>(() => PostRules.ValidatePublished(PostKind.Album, true, 0));
    }

    [Fact]
    public void Post_KindParsingIsStrict()
    {
        Assert.Equal(PostKind.Album, PostRules.ParseKind("Album"));
        foreach (string bad in new[] { "album", "Video", "", "99", null! })
        {
            Assert.Throws<BusinessRuleException>(() => PostRules.ParseKind(bad));
        }
    }

    private static PostService NewPostService(ICommsRepository comms) => new(comms, Clock);

    [Fact]
    public async Task Post_CreateGeneratesUniqueSlugAndStartsAsDraft()
    {
        var comms = Substitute.For<ICommsRepository>();
        comms.SlugExistsAsync("ngay-hoi").Returns(true);
        comms.SlugExistsAsync("ngay-hoi-2").Returns(true);
        Post? created = null;
        comms.CreatePostAsync(Arg.Do<Post>(post => { post.Id = 9; created = post; })).Returns(call => call.Arg<Post>());
        comms.GetPostAsync(9).Returns(call => created);
        var service = NewPostService(comms);

        var dto = await service.CreateAsync(Admin, new SavePostRequest
        {
            Kind = "Album", Title = "Ngày hội", Content = "Nội dung", Images = [new() { Url = "/uploads/images/a.jpg", Caption = " Chụp chung " }, new() { Url = "/uploads/images/b.jpg" }]
        });
        Assert.Equal("ngay-hoi-3", created!.Slug);
        Assert.False(created.IsPublished);
        Assert.Null(created.PublishedAt);
        Assert.Equal("/uploads/images/a.jpg", created.CoverUrl); // ảnh bìa mặc định = ảnh đầu
        Assert.Equal(["Chụp chung", null], created.Images.Select(image => image.Caption));
        Assert.Equal([0, 1], created.Images.Select(image => image.SortOrder));
        Assert.Equal("Album", dto.Kind);
    }

    [Fact]
    public async Task Post_CreateRejectsUntrustedImagesAndTooManyImages()
    {
        var service = NewPostService(Substitute.For<ICommsRepository>());
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Admin, new SavePostRequest { Kind = "News", Title = "A", Content = "B", Images = [new() { Url = "https://evil.example/a.png" }] }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Admin, new SavePostRequest { Kind = "News", Title = "A", Content = "B", CoverUrl = "javascript:alert(1)" }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Admin, new SavePostRequest
        {
            Kind = "News", Title = "A", Content = "B", Images = Enumerable.Range(0, PostRules.MaxImages + 1).Select(i => new SavePostImageRequest { Url = $"/uploads/images/{i}.jpg" }).ToList()
        }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Admin, new SavePostRequest { Kind = "Video", Title = "A", Content = "B" }));
    }

    /// <summary>DAO chạy callback trên bản đang theo dõi trong transaction; mô phỏng lại ở đây.</summary>
    private static ICommsRepository RepoWith(Post post)
    {
        var comms = Substitute.For<ICommsRepository>();
        comms.UpdatePostAsync(post.Id, Arg.Any<Action<Post>>(), Arg.Any<List<PostImage>?>()).Returns(call =>
        {
            var images = call.ArgAt<List<PostImage>?>(2);
            if (images is not null)
            {
                post.Images = images;
            }

            call.ArgAt<Action<Post>>(1)(post);
            return post;
        });
        comms.GetPostAsync(post.Id).Returns(post);
        return comms;
    }

    [Fact]
    public async Task Post_AlbumCannotBePublishedWithoutConsentOrImages()
    {
        var noConsent = new Post { Id = 1, Kind = PostKind.Album, Title = "A", Slug = "a", Content = "x", ConsentConfirmed = false, Images = [new() { Url = "/uploads/images/a.jpg" }] };
        await Assert.ThrowsAsync<BusinessRuleException>(() => NewPostService(RepoWith(noConsent)).SetPublishedAsync(1, true));
        Assert.False(noConsent.IsPublished);

        var noImages = new Post { Id = 2, Kind = PostKind.Album, Title = "A", Slug = "a", Content = "x", ConsentConfirmed = true };
        await Assert.ThrowsAsync<BusinessRuleException>(() => NewPostService(RepoWith(noImages)).SetPublishedAsync(2, true));

        var ready = new Post { Id = 3, Kind = PostKind.Album, Title = "A", Slug = "a", Content = "x", ConsentConfirmed = true, Images = [new() { Url = "/uploads/images/a.jpg" }] };
        var dto = await NewPostService(RepoWith(ready)).SetPublishedAsync(3, true);
        Assert.True(dto.IsPublished);
        Assert.NotNull(ready.PublishedAt);
    }

    [Fact]
    public async Task Post_PublishIsIdempotentAndUnpublishKeepsHistory()
    {
        var firstPublished = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var post = new Post { Id = 4, Kind = PostKind.News, Title = "A", Slug = "a", Content = "x", IsPublished = true, PublishedAt = firstPublished };
        var service = NewPostService(RepoWith(post));

        await service.SetPublishedAsync(4, true);
        Assert.Equal(firstPublished, post.PublishedAt); // đăng lại bài đang đăng không đổi ngày đăng

        await service.SetPublishedAsync(4, false);
        Assert.False(post.IsPublished);
        Assert.Equal(firstPublished, post.PublishedAt);

        await service.SetPublishedAsync(4, true);
        Assert.True(post.IsPublished);
        Assert.True(post.PublishedAt > firstPublished); // đăng lại sau khi gỡ = ngày đăng mới
    }

    [Fact]
    public async Task Post_UpdateKeepsSlugAndRefusesKindChange()
    {
        var post = new Post { Id = 5, Kind = PostKind.News, Title = "Cũ", Slug = "cu", Content = "x" };
        var service = NewPostService(RepoWith(post));

        await service.UpdateAsync(5, new SavePostRequest { Kind = "News", Title = "Mới", Content = "Nội dung mới" });
        Assert.Equal("Mới", post.Title);
        Assert.Equal("cu", post.Slug);
        Assert.NotNull(post.UpdatedAt);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(5, new SavePostRequest { Kind = "Album", Title = "Mới", Content = "x" }));
    }

    [Fact]
    public async Task Post_PublishedAlbumKeepsItsConditionsOnUpdate()
    {
        var post = new Post { Id = 6, Kind = PostKind.Album, Title = "A", Slug = "a", Content = "x", ConsentConfirmed = true, IsPublished = true, PublishedAt = DateTime.UtcNow,
            Images = [new() { Url = "/uploads/images/a.jpg" }] };
        var service = NewPostService(RepoWith(post));
        var keep = new List<SavePostImageRequest> { new() { Url = "/uploads/images/a.jpg" } };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(6, new SavePostRequest { Kind = "Album", Title = "A", Content = "x", ConsentConfirmed = false, Images = keep }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(6, new SavePostRequest { Kind = "Album", Title = "A", Content = "x", ConsentConfirmed = true, Images = [] }));
        await service.UpdateAsync(6, new SavePostRequest { Kind = "Album", Title = "A", Content = "y", ConsentConfirmed = true, Images = keep });
    }

    [Fact]
    public async Task Post_PublicLookupNormalizesSlugAndHidesUnpublished()
    {
        var comms = Substitute.For<ICommsRepository>();
        comms.GetPublishedBySlugAsync("bai-cong-khai").Returns(new Post
        {
            Id = 1, Kind = PostKind.News, Title = "Bài", Slug = "bai-cong-khai", Content = "x", IsPublished = true, PublishedAt = DateTime.UtcNow, CreatedBy = new Account { FullName = "Người tạo" }
        });
        var service = NewPostService(comms);

        var dto = await service.GetPublishedAsync(" Bai-Cong-Khai ");
        Assert.Equal("bai-cong-khai", dto.Slug);
        Assert.DoesNotContain(typeof(PublicPostDto).GetProperties(), property => property.Name is "CreatedByName" or "ConsentConfirmed" or "IsPublished");
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetPublishedAsync("khong-co"));
    }

    [Fact]
    public async Task Post_ListClampsPageSizeAndValidatesKind()
    {
        var comms = Substitute.For<ICommsRepository>();
        comms.ListPostsAsync(true, null, Arg.Any<int>(), Arg.Any<int>()).Returns(([new PostRow(new Post { Id = 1, Kind = PostKind.News, Title = "A", Slug = "a" }, 0)], 1));
        var service = NewPostService(comms);

        var page = await service.ListPublishedAsync(null, 0, 1000);
        Assert.Equal(1, page.Page);
        Assert.Equal(PostService.MaxPageSize, page.PageSize);
        await comms.Received().ListPostsAsync(true, null, 0, PostService.MaxPageSize);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ListPublishedAsync("Video", 1, 10));
    }

    // ---------------- Danh mục (UC-130) ----------------

    [Fact]
    public async Task Lookup_CategoryMustBeKnownAndCannotChange()
    {
        var system = Substitute.For<ISystemRepository>();
        system.GetLookupAsync(3).Returns(new Lookup { Id = 3, Category = LookupCategories.Position, Code = "A", Name = "A" });
        var service = new LookupService(system);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ListAsync("Foo", false));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(new SaveLookupRequest { Category = null, Code = "A", Name = "A" }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(new SaveLookupRequest { Category = "Foo", Code = "A", Name = "A" }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(3, new SaveLookupRequest { Category = LookupCategories.Department, Code = "A", Name = "A" }));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(99));
    }
}
