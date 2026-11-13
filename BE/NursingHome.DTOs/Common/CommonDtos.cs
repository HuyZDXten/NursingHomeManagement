namespace NursingHome.DTOs.Common;

/// <summary>
/// Dữ liệu người gọi lấy từ JWT ở tầng API rồi truyền vào Service, để Services không phụ thuộc
/// HttpContext (giống ASS2).
/// </summary>
public sealed record CurrentUser(int AccountId, string Role, string Email, string Name)
{
    public bool IsInRole(params string[] roles) => roles.Contains(Role);
}

/// <summary>Định dạng lỗi duy nhất của API: { "message": "..." }.</summary>
public class ErrorResponse
{
    public string Message { get; set; } = "";
}

public sealed record LookupItem(int Id, string Name, string? Extra = null);

public sealed record MessageResponse(string Message);
