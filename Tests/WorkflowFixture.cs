using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.Tests;

public sealed class WorkflowFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public string Password { get; } = "Test!" + Guid.NewGuid().ToString("N") + "9a";
    public string DatabaseName { get; } = "NursingHomeTests_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("SQL connection is required."))
        {
            InitialCatalog = DatabaseName
        };
        ConnectionString = connection.ConnectionString;
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Seed:DemoPassword"] = Password
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(ConnectionString));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(DatabaseName, database.Database.GetDbConnection().Database);
        await database.Database.MigrateAsync();
        await DbInitializer.InitializeAsync(scope.ServiceProvider, true, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoPassword"] = Password }).Build());
    }

    public ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options);

    public IServiceScope Scope(string role)
    {
        var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = database.Users.Single(user => user.Email == role.ToLowerInvariant() + "@demo.nursinghome.test");
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.Id),
                new Claim(ClaimTypes.Role, role)
            }, "test"))
        };
        return scope;
    }

    public async Task<HttpClient> LoginAsync(string role)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var response = await PostAsync(client, "/Account/Login", "/Account/Login", new Dictionary<string, string>
        {
            ["Email"] = role.ToLowerInvariant() + "@demo.nursinghome.test",
            ["Password"] = Password
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string getPath, string postPath, Dictionary<string, string> values)
    {
        string html = await client.GetStringAsync(getPath);
        string token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return await client.PostAsync(postPath, new FormUrlEncodedContent(values));
    }

    public async Task DisposeAsync()
    {
        // This fixture only drops the unique database it created; never the user's database.
        if (!DatabaseName.StartsWith("NursingHomeTests_", StringComparison.Ordinal) || DatabaseName.Length != 49)
        {
            throw new InvalidOperationException("Unexpected test database name.");
        }
        await using var database = Database();
        await database.Database.EnsureDeletedAsync();
        await Factory.DisposeAsync();
    }
}
