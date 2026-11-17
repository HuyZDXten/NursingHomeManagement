using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using NursingHome.API.Extensions;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Filters;

/// <summary>Mô tả thao tác cần ghi nhật ký (UC-13). Summary là câu tiếng Việt ngắn; mã cư dân/hóa đơn... được nối vào sau.</summary>
public sealed record AuditSpec(string Action, string EntityType, string Summary, string? IdKey);

/// <summary>
/// UC-13: đặt lên action ghi dữ liệu quan trọng. CHỈ ghi khi thao tác thành công (2xx); thao tác lỗi/bị từ chối không vào nhật ký.
/// Không ghi nội dung request (có thể chứa mật khẩu, CCCD): chỉ phương thức, đường dẫn, người thực hiện, IP và mã đối tượng.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuditAttribute : TypeFilterAttribute
{
    /// <param name="idKey">Tên giá trị route chứa mã đối tượng; bỏ trống thì thử các tên thông dụng (id, residentId...) rồi lấy Id trong kết quả.</param>
    public AuditAttribute(string action, string entityType, string summary, string? idKey = null) : base(typeof(AuditFilter))
    {
        Arguments = [new AuditSpec(action, entityType, summary, idKey)];
    }
}

public sealed class AuditFilter(AuditSpec spec, IAuditService audit) : IAsyncActionFilter
{
    private static readonly string[] CommonIdKeys = ["id", "accountId", "residentId", "invoiceId", "depositId", "contractId", "leaveId", "documentId", "key"];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return; // lỗi nghiệp vụ/hệ thống: ExceptionHandlingMiddleware xử lý, không ghi
        }

        int status = executed.Result is IStatusCodeActionResult { StatusCode: { } code } ? code : StatusCodes.Status200OK;
        if (status >= StatusCodes.Status400BadRequest)
        {
            return;
        }

        var http = context.HttpContext;
        string? entityId = FindEntityId(context, executed);
        string summary = entityId is null ? spec.Summary : $"{spec.Summary} #{entityId}";
        var data = new { Method = http.Request.Method, Path = http.Request.Path.Value };
        await audit.RecordAsync(http.User.ToCurrentUser(), spec.Action, spec.EntityType, entityId, summary, data,
            http.Connection.RemoteIpAddress?.ToString());
    }

    private string? FindEntityId(ActionExecutingContext context, ActionExecutedContext executed)
    {
        var routeValues = context.RouteData.Values;
        foreach (string key in spec.IdKey is null ? CommonIdKeys : [spec.IdKey])
        {
            if (routeValues.TryGetValue(key, out var value) && value is not null)
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        // POST tạo mới: mã nằm trong kết quả trả về.
        object? body = (executed.Result as ObjectResult)?.Value;
        var idProperty = body?.GetType().GetProperty("Id");
        return idProperty?.GetValue(body) is { } id ? Convert.ToString(id, System.Globalization.CultureInfo.InvariantCulture) : null;
    }
}
