using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Chat;
using NursingHome.DTOs.Common;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// MODULE VẬN HÀNH (Viên) — S3-V6: UC-106 chat gia đình – nhân viên. Quyền kiểm ở ChatService
/// (Family: người thân được liên kết; nhân viên: Admin, Receptionist, Nurse, Caregiver phụ trách chính).
/// </summary>
[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController(IChatService chat) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ConversationDto>>> List() => Ok(await chat.ListAsync(User.RequireCurrentUser()));

    /// <summary>Gia đình mở (hoặc lấy lại) cuộc trò chuyện về một người thân.</summary>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.FamilyOnly)]
    public async Task<ActionResult<ConversationDto>> Open([FromBody] OpenConversationRequest request) =>
        Ok(await chat.OpenAsync(User.RequireCurrentUser(), request.ResidentId));

    /// <param name="beforeId">Tải tin cũ hơn tin này (cuộn lên).</param>
    [HttpGet("{id:int}/messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> Messages(int id, [FromQuery] long? beforeId, [FromQuery] int take = 30) =>
        Ok(await chat.MessagesAsync(User.RequireCurrentUser(), id, beforeId, take));

    [HttpPost("{id:int}/messages")]
    [EnableRateLimiting(RateLimitPolicies.Chat)]
    public async Task<ActionResult<ChatMessageDto>> Send(int id, [FromBody] SendMessageRequest request) =>
        Ok(await chat.SendAsync(User.RequireCurrentUser(), id, request));

    [HttpPost("{id:int}/read")]
    public async Task<ActionResult<MessageResponse>> Read(int id)
    {
        var count = await chat.MarkReadAsync(User.RequireCurrentUser(), id);
        return Ok(new MessageResponse($"Đã đánh dấu {count} tin là đã đọc."));
    }
}
