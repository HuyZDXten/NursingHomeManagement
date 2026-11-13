using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NursingHome.API.BackgroundJobs;
using NursingHome.API.Extensions;
using NursingHome.API.Hubs;
using NursingHome.API.Middleware;
using NursingHome.API.OData;
using NursingHome.API.Security;
using NursingHome.BusinessObjects;
using NursingHome.DTOs.Auth;
using NursingHome.Services;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

// ---- Lệnh tiện ích (không khởi động web) ----
//   dotnet run -- --hash-password "Demo@123456"   in hash BCrypt để đưa vào Database/*.sql
//   dotnet run -- --check-db                      kiểm tra kết nối DB (database-first: API không tạo/migrate DB)
if (args.Length >= 2 && args[0] == "--hash-password")
{
    Console.WriteLine(PasswordHasher.Hash(args[1]));
    return;
}

// Định dạng số/tiền trong thông báo kiểu Việt Nam (6.300.000 đ). JSON và query string vẫn dùng invariant culture.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("vi-VN");

var builder = WebApplication.CreateBuilder(args);

if (args.Contains("--check-db"))
{
    await using var context = new NursingHomeContext();
    Console.WriteLine(await context.Database.CanConnectAsync()
        ? $"Kết nối OK. Accounts: {await context.Accounts.CountAsync()}, Residents: {await context.Residents.CountAsync()}"
        : "Không kết nối được SQL Server. Kiểm tra ConnectionStrings:DefaultConnection và chạy Database/setup-local-db.ps1.");
    return;
}

// ---- Options: thiếu/yếu khóa JWT thì dừng ngay khi khởi động ----
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => Encoding.UTF8.GetByteCount(options.Key) >= 32,
        "Jwt:Key phải dài ít nhất 32 byte. Tạo BE/NursingHome.API/appsettings.Development.json từ appsettings.Development.example.json.")
    .Validate(options => options.AccessTokenMinutes > 0 && options.RefreshTokenDays > 0, "Thời hạn token phải dương.")
    .ValidateOnStart();

// ---- Repositories + Services (NursingHome.Services/ServiceCollectionExtensions.cs) ----
builder.Services.AddNursingHomeServices(builder.Configuration);
builder.Services.AddSingleton<INotificationPublisher, SignalRNotificationPublisher>();
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddHostedService<PayOsSyncService>();
builder.Services.AddHostedService<InvoiceReminderJob>(); // S3-V5 nhắc nợ mỗi giờ
builder.Services.AddSignalR();

// ---- Auth: JWT bearer (giống ASS2) ----
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AuthClaimTypes.Name,
            RoleClaimType = AuthClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            // SignalR (WebSocket) không gửi header được nên token đi qua query ?access_token=.
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                bool expired = context.AuthenticateFailure is SecurityTokenExpiredException;
                if (expired)
                {
                    context.Response.Headers["Token-Expired"] = "true";
                }

                await ExceptionHandlingMiddleware.WriteErrorAsync(context.Response, StatusCodes.Status401Unauthorized,
                    expired ? "Phiên đăng nhập đã hết hạn." : "Bạn cần đăng nhập để thực hiện thao tác này.");
            },
            OnForbidden = context => ExceptionHandlingMiddleware.WriteErrorAsync(
                context.Response, StatusCodes.Status403Forbidden, "Bạn không có quyền thực hiện thao tác này.")
        };
    });

var authorization = builder.Services.AddAuthorizationBuilder();
foreach (var (policy, roles) in AuthPolicies.Map)
{
    authorization.AddPolicy(policy, rule => rule.RequireRole(roles));
}

// ---- Rate limit cho đăng nhập/OTP/đổi mật khẩu và form công khai ----
int credentialsPerMinute = builder.Configuration.GetValue("RateLimit:CredentialsPerMinute", 10);
int publicFormsPerMinute = builder.Configuration.GetValue("RateLimit:PublicFormsPerMinute", 5);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Credentials, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = credentialsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy(RateLimitPolicies.PublicForms, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = publicFormsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = (context, _) => new ValueTask(ExceptionHandlingMiddleware.WriteErrorAsync(
        context.HttpContext.Response, StatusCodes.Status429TooManyRequests, "Thao tác quá nhiều lần. Vui lòng chờ một phút."));
});

// ---- CORS cho FE (Vite) ----
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("Token-Expired")));

// ---- Controllers + OData ----
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddOData(options =>
    {
        options.Select().Filter().OrderBy().Expand().Count().SetMaxTop(200)
            .AddRouteComponents("odata", EdmModelBuilder.Build());
        // Chỉ giữ attribute routing ([Route("odata/X")] trên ODataController) + $metadata. Convention theo TÊN controller
        // bắt nhầm controller REST trùng tên entity set (InvoicesController.Get(id) nhận GET /odata/Invoices với id = 0 → 404).
        foreach (var convention in options.Conventions
                     .Where(convention => convention is not AttributeRoutingConvention and not MetadataRoutingConvention).ToList())
        {
            options.Conventions.Remove(convention);
        }
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Nursing Home Management API (OData + JWT)", Version = "v1" });
    options.ResolveConflictingActions(descriptions => descriptions.First());
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán accessToken lấy từ POST /api/auth/login (không cần chữ \"Bearer \")."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

// Development: FE gọi http thuần; redirect https sẽ làm hỏng CORS preflight.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<NotificationHub>(NotificationHub.Path);

app.Run();

public partial class Program;
