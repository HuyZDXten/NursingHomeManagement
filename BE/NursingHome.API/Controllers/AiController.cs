using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NursingHome.API.Security;
using NursingHome.DTOs.Ai;
using NursingHome.Services.Ai;

namespace NursingHome.API.Controllers;

/// <summary>
/// MODULE VẬN HÀNH (Viên) — S4-V1: UC-20 chatbot tư vấn công khai (ẩn danh, rate limit AiPublic theo IP) và quản lý
/// tài liệu tri thức (Admin). Trợ lý gia đình (S4-H1) của Huy có controller riêng trong module Care.
/// </summary>
[ApiController]
[Route("api/ai")]
public class AiController(IPublicChatbotService chatbot) : ControllerBase
{
    [HttpGet("public/config")]
    [AllowAnonymous]
    public ActionResult<AiPublicConfigDto> Config() => Ok(chatbot.GetConfig());

    [HttpPost("public/chat")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AiPublic)]
    public async Task<ActionResult<AiChatReplyDto>> Chat([FromBody] AiPublicChatRequest request, CancellationToken cancellationToken) =>
        Ok(await chatbot.ChatAsync(request, cancellationToken));

    [HttpGet("public/history")]
    [AllowAnonymous]
    public async Task<ActionResult<List<AiHistoryItemDto>>> History([FromQuery] string conversationKey) =>
        Ok(await chatbot.HistoryAsync(conversationKey));

    [HttpGet("knowledge")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<List<AiKnowledgeDocDto>>> Knowledge() => Ok(await chatbot.ListKnowledgeAsync());

    [HttpPost("knowledge")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<AiKnowledgeDocDto>> CreateKnowledge([FromBody] SaveAiKnowledgeRequest request) =>
        Ok(await chatbot.SaveKnowledgeAsync(null, request));

    [HttpPut("knowledge/{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<AiKnowledgeDocDto>> UpdateKnowledge(int id, [FromBody] SaveAiKnowledgeRequest request) =>
        Ok(await chatbot.SaveKnowledgeAsync(id, request));

    [HttpDelete("knowledge/{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> DeleteKnowledge(int id)
    {
        await chatbot.DeleteKnowledgeAsync(id);
        return NoContent();
    }
}
