using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Extensions;
using NursingHomeManagement.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNursingHome(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    // Keep development keys outside source and wwwroot; production uses the host's protected key store.
    var keyDirectory = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "..", "work", "keys"));
    var protection = builder.Services.AddDataProtection().PersistKeysToFileSystem(keyDirectory);
    if (OperatingSystem.IsWindows())
    {
        protection.ProtectKeysWithDpapi();
    }
}
var app = builder.Build();
if (args.Contains("--initialize-db") || args.Contains("--check-db"))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (args.Contains("--initialize-db"))
    {
        bool demo = builder.Configuration.GetValue<bool>("Seed:DemoEnabled");
        if (demo && !app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Demo seed is only allowed in Development.");
        }

        await database.Database.MigrateAsync();
        await DbInitializer.InitializeAsync(scope.ServiceProvider, demo, builder.Configuration);
    }

    if (!await database.Database.CanConnectAsync())
    {
        throw new InvalidOperationException("Cannot connect to SQL Server.");
    }

    Console.WriteLine($"Database connection OK. Residents: {await database.Residents.CountAsync()}, roles: {await database.Roles.CountAsync()}");
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AccessExceptionMiddleware>();
app.MapStaticAssets().AllowAnonymous();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
public partial class Program;
