using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;

namespace NursingHomeManagement.Tests;

public class FoundationTests
{
    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Development");
        b.ConfigureServices(s => s.AddDataProtection().UseEphemeralDataProtectionProvider());
    });
    private static HttpClient Client(WebApplicationFactory<Program> f) => f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    [Fact]
    public async Task AnonymousCannotReadStaffPages()
    {
        using var f = Factory();
        using var c = Client(f);
        foreach (var path in new[]
        {
            "/",
            "/Family",
            "/Caregiver"
        }

        )
        {
            var response = await c.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task LoginRendersAntiforgeryAndRejectsMissingToken()
    {
        using var f = Factory();
        using var c = Client(f);
        var html = await c.GetStringAsync("/Account/Login");
        Assert.Contains("__RequestVerificationToken", html);
        var response = await c.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { { "Email", "admin@demo.nursinghome.test" }, { "Password", "invalid" } }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("admin", "/")]
    [InlineData("receptionist", "/")]
    [InlineData("caregiver", "/Caregiver")]
    [InlineData("family", "/Family")]
    public async Task DemoLoginRedirectsByRoleAndLogoutRequiresPost(string role, string expected)
    {
        using var f = Factory();
        using var c = Client(f);
        var html = await c.GetStringAsync("/Account/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var password = Environment.GetEnvironmentVariable("Seed__DemoPassword") ?? throw new InvalidOperationException("Set Seed__DemoPassword for SQL integration tests.");
        var response = await c.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { { "Email", role + "@demo.nursinghome.test" }, { "Password", password }, { "__RequestVerificationToken", WebUtility.HtmlDecode(token) }, { "ReturnUrl", "https://evil.example" } }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location!.ToString());
        var page = await c.GetAsync(expected);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        if (role == "family" || role == "caregiver")
        {
            var denied = await c.GetAsync("/");
            Assert.Contains("AccessDenied", denied.Headers.Location!.ToString());
        }

        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Account/Logout")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(expected)).StatusCode);
        var body = await page.Content.ReadAsStringAsync();
        token = Regex.Match(body, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { { "__RequestVerificationToken", WebUtility.HtmlDecode(token) } }))).StatusCode);
        Assert.Contains("/Account/Login", (await c.GetAsync(expected)).Headers.Location!.ToString());
    }

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? throw new InvalidOperationException("Set SQL connection for integration tests.")).Options);
    [Fact]
    public async Task FamilyGuardRejectsUnlinkedResidentAndUnsharedHealth()
    {
        await using var db = Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var family = await db.Users.SingleAsync(u => u.Email == "family@demo.nursinghome.test");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, family.Id), new Claim(ClaimTypes.Role, Roles.Family) }, "test"));
        var resident = await db.Residents.SingleAsync(r => r.Code == "DEMO-001");
        var other = new Resident
        {
            Code = Guid.NewGuid().ToString("N")[..20],
            FullName = "Test only",
            DateOfBirth = new DateOnly(1940, 1, 1)
        };
        db.Add(other);
        await db.SaveChangesAsync();
        var guard = new ResidentAccessService(db);
        Assert.True(await guard.CanReadAsync(principal, resident.Id, ResidentSection.Basic));
        Assert.False(await guard.CanReadAsync(principal, resident.Id, ResidentSection.Health));
        Assert.False(await guard.CanReadAsync(principal, other.Id, ResidentSection.Basic));
        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SqlRejectsDuplicateActiveBedOrResident(bool sameBed)
    {
        await using var db = Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await db.Users.FirstAsync();
        var room = new Room
        {
            Number = Guid.NewGuid().ToString("N")[..20]
        };
        var bed1 = new Bed
        {
            Room = room,
            Number = "1"
        };
        var bed2 = new Bed
        {
            Room = room,
            Number = "2"
        };
        var r1 = new Resident
        {
            Code = Guid.NewGuid().ToString("N")[..20],
            FullName = "Test A"
        };
        var r2 = new Resident
        {
            Code = Guid.NewGuid().ToString("N")[..20],
            FullName = "Test B"
        };
        db.AddRange(bed1, bed2, r1, r2);
        await db.SaveChangesAsync();
        db.Add(new BedAssignment { Resident = r1, Bed = bed1, AssignedById = user.Id });
        await db.SaveChangesAsync();
        db.Add(new BedAssignment { Resident = sameBed ? r2 : r1, Bed = sameBed ? bed1 : bed2, AssignedById = user.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task SqlRejectsDuplicateBillingPeriod()
    {
        await using var db = Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = new Resident
        {
            Code = Guid.NewGuid().ToString("N")[..20],
            FullName = "Test invoice"
        };
        db.Add(new Invoice { Resident = r, Number = Guid.NewGuid().ToString(), BillingPeriod = new DateOnly(2026, 11, 1), DueDate = new DateOnly(2026, 11, 30) });
        await db.SaveChangesAsync();
        db.Add(new Invoice { Resident = r, Number = Guid.NewGuid().ToString(), BillingPeriod = new DateOnly(2026, 11, 1), DueDate = new DateOnly(2026, 11, 30) });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await tx.RollbackAsync();
    }
}
