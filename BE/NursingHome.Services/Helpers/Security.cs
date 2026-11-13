using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NursingHome.Services.Helpers;

/// <summary>BCrypt (work factor mặc định 11). Seed SQL phải dùng hash sinh từ chính lớp này:
/// <c>dotnet run --project NursingHome.API -- --hash-password "MatKhau@123"</c>.</summary>
public static class PasswordHasher
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public static bool Verify(string password, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash) || !storedHash.StartsWith("$2", StringComparison.Ordinal))
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(password, storedHash);
    }
}

/// <summary>
/// Refresh token: 64 byte ngẫu nhiên, Base64Url. DB chỉ lưu SHA-256 (đầu vào ngẫu nhiên nên hash nhanh là đủ).
/// </summary>
public static class RefreshTokenGenerator
{
    public static string Create() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>
/// OTP quên mật khẩu (UC-05) giữ trong bộ nhớ như ASS2: 6 chữ số, hết hạn 10 phút,
/// gửi lại sau 60 giây, sai 5 lần thì hủy. Đăng ký Singleton.
/// </summary>
public sealed class PasswordResetOtpStore(TimeProvider time)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);
    private const int MaxAttempts = 5;

    private sealed record Entry(string CodeHash, DateTimeOffset IssuedAt, int Attempts);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Trả null khi còn trong thời gian chờ gửi lại.</summary>
    public string? Issue(string email)
    {
        var now = time.GetUtcNow();
        if (_entries.TryGetValue(email, out var existing) && now - existing.IssuedAt < Cooldown)
        {
            return null;
        }

        string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _entries[email] = new Entry(HashCode(code), now, 0);
        return code;
    }

    public bool Verify(string email, string code)
    {
        if (!_entries.TryGetValue(email, out var entry))
        {
            return false;
        }

        if (time.GetUtcNow() - entry.IssuedAt > Lifetime || entry.Attempts >= MaxAttempts)
        {
            _entries.TryRemove(email, out _);
            return false;
        }

        bool ok = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(entry.CodeHash), Encoding.ASCII.GetBytes(HashCode(code)));
        if (ok)
        {
            _entries.TryRemove(email, out _);
        }
        else
        {
            _entries[email] = entry with { Attempts = entry.Attempts + 1 };
        }

        return ok;
    }

    private static string HashCode(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}

/// <summary>
/// Cơ sở dùng giờ Việt Nam (UTC+7); DB lưu UTC. Mọi "hôm nay", "giờ uống thuốc" quy đổi qua đây.
/// </summary>
public sealed class FacilityClock(TimeProvider time)
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow + Offset);

    /// <summary>Ngày + giờ theo giờ cơ sở → thời điểm UTC.</summary>
    public DateTime ToUtc(DateOnly date, TimeOnly timeOfDay) =>
        DateTime.SpecifyKind(date.ToDateTime(timeOfDay) - Offset, DateTimeKind.Utc);

    public DateTime StartOfDayUtc(DateOnly date) => ToUtc(date, TimeOnly.MinValue);

    public static DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(utc + Offset);
}
