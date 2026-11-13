using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;

namespace NursingHome.API.Middleware;

/// <summary>
/// Đổi exception thành một định dạng lỗi duy nhất { "message": "..." } (giống ASS2).
/// Thêm: lỗi tranh chấp dữ liệu của SQL Server (rowversion, unique index, deadlock Serializable) → 409.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        // Giữ nguyên tiếng Việt trong thông báo lỗi (mặc định sẽ thành á...).
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, message) = ex switch
            {
                NotFoundException => (HttpStatusCode.NotFound, ex.Message),
                ConflictException => (HttpStatusCode.Conflict, ex.Message),
                BusinessRuleException => (HttpStatusCode.BadRequest, ex.Message),
                UnauthorizedServiceException => (HttpStatusCode.Unauthorized, ex.Message),
                ForbiddenException => (HttpStatusCode.Forbidden, ex.Message),
                TooManyRequestsException => (HttpStatusCode.TooManyRequests, ex.Message),
                ServiceUnavailableException => (HttpStatusCode.ServiceUnavailable, ex.Message),
                DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "Dữ liệu vừa được người khác cập nhật. Vui lòng tải lại và thử lại."),
                DbUpdateException { InnerException: SqlException sql } => MapSql(sql),
                SqlException sql => MapSql(sql),
                _ => (HttpStatusCode.InternalServerError, "Đã xảy ra lỗi không mong muốn.")
            };

            if (status == HttpStatusCode.InternalServerError)
            {
                logger.LogError(ex, "Unhandled exception");
            }

            await WriteErrorAsync(context.Response, (int)status, message);
        }
    }

    private static (HttpStatusCode, string) MapSql(SqlException sql) => sql.Number switch
    {
        2601 or 2627 => (HttpStatusCode.Conflict, "Dữ liệu bị trùng (đã tồn tại hoặc vừa được người khác tạo)."),
        547 => (HttpStatusCode.Conflict, "Dữ liệu vi phạm ràng buộc hoặc đang được sử dụng."),
        1205 => (HttpStatusCode.Conflict, "Có thao tác đồng thời trên cùng dữ liệu. Vui lòng thử lại."),
        _ => (HttpStatusCode.InternalServerError, "Lỗi cơ sở dữ liệu.")
    };

    /// <summary>Dùng chung cho JWT challenge/forbidden và rate-limit để FE đọc lỗi một kiểu.</summary>
    public static Task WriteErrorAsync(HttpResponse response, int statusCode, string message)
    {
        response.ContentType = "application/json; charset=utf-8";
        response.StatusCode = statusCode;
        return response.WriteAsync(JsonSerializer.Serialize(new ErrorResponse { Message = message }, SerializerOptions));
    }
}
