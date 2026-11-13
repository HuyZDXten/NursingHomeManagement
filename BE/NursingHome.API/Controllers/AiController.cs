using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Ai;
using NursingHome.Services.Ai;

namespace NursingHome.API.Controllers;

/// <summary>
/// MODULE VẬN HÀNH (Viên) — S4-V1: UC-20 chatbot tư vấn công khai (ẩn danh, rate limit AiPublic theo IP) và quản lý
/// tài liệu tri thức (Admin); S4-V2: AI-12 hỏi đáp báo cáo (Admin, Kế toán). Trợ lý gia đình (S4-H1) của Huy có
/// controller riêng trong module Care.
/// </summary>
[ApiController]
[Route("api/ai")]
public class AiController(IPublicChatbotService chatbot, IReportAssistantService reportAssistant) : ControllerBase
{
    /// <summary>AI-12: mỗi tool tự kiểm quyền theo policy của báo cáo tương ứng (Kế toán không xem được tuân thủ thuốc).</summary>
    [HttpPost("staff/report-chat")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    [EnableRateLimiting(RateLimitPolicies.Chat)]
    public async Task<ActionResult<AiChatReplyDto>> ReportChat([FromBody] AiStaffChatRequest request, CancellationToken cancellationToken) =>
        Ok(await reportAssistant.ChatAsync(User.RequireCurrentUser(), request.Message, cancellationToken));

    [HttpGet("staff/report-chat/history")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    public async Task<ActionResult<List<AiHistoryItemDto>>> ReportChatHistory() =>
        Ok(await reportAssistant.HistoryAsync(User.RequireCurrentUser()));

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
