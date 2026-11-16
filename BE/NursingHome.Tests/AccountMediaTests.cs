using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Accounts;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Tests;

/// <summary>BL-V1: kiểm ảnh tải lên, mã mời gia đình, đăng nhập Google.</summary>
public class AccountMediaTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    private static readonly byte[] Webp = [.. "RIFF"u8.ToArray(), 0x24, 0x00, 0x00, 0x00, .. "WEBP"u8.ToArray()];

    // ---------------- Ảnh ----------------

    [Fact]
    public void Image_DetectsFormatFromSignature()
    {
        Assert.Equal(".png", ImageRules.DetectExtension(Png));
        Assert.Equal(".jpg", ImageRules.DetectExtension(Jpeg));
        Assert.Equal(".webp", ImageRules.DetectExtension(Webp));
        Assert.Null(ImageRules.DetectExtension("<html>alert</html>"u8));
        Assert.Null(ImageRules.DetectExtension("GIF89a......"u8));
    }

    [Fact]
    public void Image_StoredExtensionFollowsContentNotFileName()
    {
        // Tên .png nhưng nội dung JPEG → lưu .jpg (không bao giờ phục vụ sai kiểu).
        Assert.Equal(".jpg", ImageRules.Validate("anh.PNG", Jpeg.Length, Jpeg));
    }

    [Theory]
    [InlineData("anh.gif", 100, "Chỉ nhận ảnh JPG, PNG hoặc WebP.")]
    [InlineData("anh.png", 0, "Chưa chọn ảnh.")]
    [InlineData("anh.png", ImageRules.MaxBytes + 1, "Ảnh tối đa 5 MB.")]
    public void Image_RejectsBadFiles(string name, long length, string message)
    {
        var error = Assert.Throws<BusinessRuleException>(() => ImageRules.Validate(name, length, Png));
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Image_RejectsHtmlRenamedAsPng()
    {
        var html = "<html><script>x</script>"u8.ToArray();
        Assert.Throws<BusinessRuleException>(() => ImageRules.Validate("x.png", html.Length, html));
    }

    // ---------------- Mã mời ----------------

    [Fact]
    public void Invitation_CodesAreUrlSafeAndUnique()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => InvitationRules.NewCode()).ToList();
        Assert.All(codes, code => Assert.Matches("^[A-Za-z0-9_-]{24}$", code));
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void Invitation_HashIsUppercaseSha256LikeSqlSeed()
    {
        string hash = InvitationRules.Hash(" DEMO-NH0003-MOI ");
        Assert.Matches("^[0-9A-F]{64}$", hash);
        Assert.Equal(hash, InvitationRules.Hash("DEMO-NH0003-MOI"));
        Assert.NotEqual(hash, InvitationRules.Hash("demo-nh0003-moi"));
    }

    [Fact]
    public void Invitation_StatusPrefersUsedThenRevokedThenExpired()
    {
        var now = new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);
        var pending = new FamilyInvitation { ExpiresAt = now.AddDays(1) };
        Assert.Equal(InvitationRules.Pending, InvitationRules.StatusOf(pending, now));
        Assert.Equal(InvitationRules.Expired, InvitationRules.StatusOf(new FamilyInvitation { ExpiresAt = now }, now));
        Assert.Equal(InvitationRules.Revoked, InvitationRules.StatusOf(new FamilyInvitation { ExpiresAt = now.AddDays(-1), RevokedAt = now }, now));
        Assert.Equal(InvitationRules.Used, InvitationRules.StatusOf(new FamilyInvitation { ExpiresAt = now.AddDays(-1), UsedAt = now, AccountId = 9 }, now));
        Assert.Null(InvitationRules.UnusableReason(InvitationRules.Pending));
        Assert.NotNull(InvitationRules.UnusableReason(InvitationRules.Expired));
    }

    [Fact]
    public void Invitation_LinkEscapesCode()
    {
        Assert.Equal("http://localhost:5173/kich-hoat?code=a%2Bb", InvitationRules.Link("http://localhost:5173/", "a+b"));
    }

    // ---------------- Đăng nhập Google ----------------

    private static (AuthService Service, IAccountRepository Accounts, IGoogleTokenValidator Google) GoogleAuth(string clientId = "client-id")
    {
        var accounts = Substitute.For<IAccountRepository>();
        var google = Substitute.For<IGoogleTokenValidator>();
        var service = new AuthService(
            accounts,
            Substitute.For<IRefreshTokenRepository>(),
            Substitute.For<IEmailService>(),
            new PasswordResetOtpStore(TimeProvider.System),
            new FacilityClock(TimeProvider.System),
            Options.Create(new JwtOptions { Key = new string('k', 48), Issuer = "test", Audience = "test" }),
            NullLogger<AuthService>.Instance,
            google,
            Options.Create(new GoogleOptions { ClientId = clientId }));
        return (service, accounts, google);
    }

    [Fact]
    public async Task Google_ExistingActiveAccountGetsTokens()
    {
        var (service, accounts, google) = GoogleAuth();
        google.ValidateAsync("token").Returns(new GoogleIdentity("sub", "nurse@nursinghome.local", true, "Nurse"));
        accounts.GetByEmailAsync("nurse@nursinghome.local").Returns(new Account { Id = 5, Email = "nurse@nursinghome.local", FullName = "Y tá", Role = Roles.Nurse, IsActive = true });

        var session = await service.GoogleLoginAsync(new GoogleLoginRequest { IdToken = "token" });

        Assert.Equal(5, session.AccountId);
        Assert.Equal(Roles.Nurse, session.Role);
        Assert.False(string.IsNullOrEmpty(session.AccessToken));
    }

    [Fact]
    public async Task Google_UnknownEmailIsNotAutoRegistered()
    {
        var (service, accounts, google) = GoogleAuth();
        google.ValidateAsync("token").Returns(new GoogleIdentity("sub", "la@gmail.com", true, null));
        accounts.GetByEmailAsync("la@gmail.com").Returns((Account?)null);

        await Assert.ThrowsAsync<UnauthorizedServiceException>(() => service.GoogleLoginAsync(new GoogleLoginRequest { IdToken = "token" }));
        await accounts.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task Google_UnverifiedEmailOrLockedAccountRejected()
    {
        var (service, accounts, google) = GoogleAuth();
        google.ValidateAsync("unverified").Returns(new GoogleIdentity("sub", "nurse@nursinghome.local", false, null));
        google.ValidateAsync("locked").Returns(new GoogleIdentity("sub", "khoa@nursinghome.local", true, null));
        accounts.GetByEmailAsync("khoa@nursinghome.local").Returns(new Account { Id = 6, Email = "khoa@nursinghome.local", Role = Roles.Nurse, IsActive = false });

        await Assert.ThrowsAsync<UnauthorizedServiceException>(() => service.GoogleLoginAsync(new GoogleLoginRequest { IdToken = "unverified" }));
        await Assert.ThrowsAsync<UnauthorizedServiceException>(() => service.GoogleLoginAsync(new GoogleLoginRequest { IdToken = "locked" }));
        await accounts.DidNotReceive().GetByEmailAsync("nurse@nursinghome.local");
    }

    [Fact]
    public void Google_ConfigHidesButtonWhenClientIdMissing()
    {
        Assert.Equal("", GoogleAuth(clientId: "  ").Service.GetPublicConfig().GoogleClientId);
        Assert.Equal("abc.apps.googleusercontent.com", GoogleAuth(clientId: " abc.apps.googleusercontent.com ").Service.GetPublicConfig().GoogleClientId);
    }

    [Fact]
    public async Task Google_ValidatorWithoutClientIdIs503()
    {
        var validator = new GoogleTokenValidator(Options.Create(new GoogleOptions()), NullLogger<GoogleTokenValidator>.Instance);
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => validator.ValidateAsync("x"));
    }
}
