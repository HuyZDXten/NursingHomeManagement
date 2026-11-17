using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Filters;
using NursingHome.API.Security;
using NursingHome.DTOs.Admin;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// BL-V5 truyền thông: thông báo hàng loạt (UC-119, Admin), kênh nhận email của từng người (UC-120),
/// tin tức &amp; album (UC-19, UC-115: Admin + Lễ tân quản trị, trang công khai không cần đăng nhập).
/// </summary>
[ApiController]
public class CommsController(IAnnouncementService announcements, INotificationPreferenceService preferences, IPostService posts) : ControllerBase
{
    // ---------------- UC-119 thông báo hàng loạt ----------------

    [HttpGet("api/announcements")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<List<AnnouncementDto>>> Announcements() => Ok(await announcements.ListAsync());

    /// <summary>Luôn có thông báo trong ứng dụng; email chỉ gửi khi tick SendEmail, đã cấu hình SMTP và người nhận chưa tắt kênh.</summary>
    [HttpPost("api/announcements")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [Audit("Announcement.Send", "Announcement", "Gửi thông báo hàng loạt")]
    public async Task<ActionResult<AnnouncementDto>> SendAnnouncement([FromBody] CreateAnnouncementRequest request) =>
        Ok(await announcements.SendAsync(User.RequireCurrentUser(), request));

    // ---------------- UC-120 kênh nhận ----------------

    [HttpGet("api/notification-preferences/me")]
    [Authorize]
    public async Task<ActionResult<NotificationPreferenceDto>> MyPreferences() => Ok(await preferences.GetAsync(User.RequireCurrentUser()));

    [HttpPut("api/notification-preferences/me")]
    [Authorize]
    public async Task<ActionResult<NotificationPreferenceDto>> SaveMyPreferences([FromBody] NotificationPreferenceDto request) =>
        Ok(await preferences.SaveAsync(User.RequireCurrentUser(), request));

    // ---------------- UC-19 tin tức, UC-115 album (quản trị) ----------------

    /// <summary>Gồm cả nháp. kind = News | Album (bỏ trống = cả hai).</summary>
    [HttpGet("api/posts")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<PageDto<PostSummaryDto>>> Posts([FromQuery] string? kind, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await posts.ListAsync(kind, page, pageSize));

    [HttpGet("api/posts/{id:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<PostDto>> Post(int id) => Ok(await posts.GetAsync(id));

    /// <summary>Tạo bài ở dạng nháp. Ảnh dùng URL đã tải qua POST /api/uploads/image.</summary>
    [HttpPost("api/posts")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    [Audit("Post.Create", "Post", "Tạo bài viết")]
    public async Task<ActionResult<PostDto>> CreatePost([FromBody] SavePostRequest request) =>
        Ok(await posts.CreateAsync(User.RequireCurrentUser(), request));

    [HttpPut("api/posts/{id:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    [Audit("Post.Update", "Post", "Sửa bài viết")]
    public async Task<ActionResult<PostDto>> UpdatePost(int id, [FromBody] SavePostRequest request) => Ok(await posts.UpdateAsync(id, request));

    /// <summary>Album chỉ đăng được khi đã xác nhận đồng ý và có ít nhất một ảnh.</summary>
    [HttpPost("api/posts/{id:int}/publish")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    [Audit("Post.Publish", "Post", "Đăng bài viết")]
    public async Task<ActionResult<PostDto>> Publish(int id) => Ok(await posts.SetPublishedAsync(id, true));

    [HttpPost("api/posts/{id:int}/unpublish")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    [Audit("Post.Unpublish", "Post", "Gỡ bài viết")]
    public async Task<ActionResult<PostDto>> Unpublish(int id) => Ok(await posts.SetPublishedAsync(id, false));

    [HttpDelete("api/posts/{id:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    [Audit("Post.Delete", "Post", "Xóa bài viết")]
    public async Task<IActionResult> DeletePost(int id)
    {
        await posts.DeleteAsync(id);
        return NoContent();
    }

    // ---------------- Trang công khai ----------------

    [HttpGet("api/public/posts")]
    [AllowAnonymous]
    public async Task<ActionResult<PageDto<PostSummaryDto>>> PublicPosts([FromQuery] string? kind, [FromQuery] int page = 1, [FromQuery] int pageSize = 12) =>
        Ok(await posts.ListPublishedAsync(kind, page, pageSize));

    [HttpGet("api/public/posts/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicPostDto>> PublicPost(string slug) => Ok(await posts.GetPublishedAsync(slug));
}
