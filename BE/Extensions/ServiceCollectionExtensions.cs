using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;

namespace NursingHomeManagement.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNursingHome(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Missing SQL Server connection string.");
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;
        });
        // Role changes and account deactivation invalidate existing cookies on the next request.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.AddAuthorization();
        services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
        services.AddHttpContextAccessor();
        services.AddSingleton<FacilityClock>();
        services.AddScoped<CurrentUser>();
        services.AddScoped<ResidentAccessService>();
        services.AddScoped<ResidentService>();
        services.AddScoped<RoomService>();
        services.AddScoped<LookupService>();
        services.AddScoped<HealthService>();
        services.AddScoped<CareTaskService>();
        services.AddScoped<MedicationService>();
        services.AddScoped<BillingService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<FamilyService>();
        services.AddScoped<UserAccountService>();
        return services;
    }
}
