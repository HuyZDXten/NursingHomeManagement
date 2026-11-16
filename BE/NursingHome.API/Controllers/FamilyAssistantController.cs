using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Ai;
using NursingHome.Services.Ai;

namespace NursingHome.API.Controllers;

/// <summary>S4-H1 (AI-02): trợ lý AI cho gia đình.</summary>
[ApiController]
[Route("api/ai/family")]
[Authorize(Policy = AuthPolicies.FamilyOnly)]
public class FamilyAssistantController(IFamilyAssistantService assistant) : ControllerBase
{
    [HttpPost("chat")]
    [EnableRateLimiting(RateLimitPolicies.AiFamily)]
    public async Task<ActionResult<AiChatReplyDto>> Chat(
        [FromBody] AiStaffChatRequest request,
        CancellationToken cancellationToken) =>
        Ok(await assistant.ChatAsync(User.RequireCurrentUser(), request.Message, cancellationToken));

    [HttpGet("history")]
    public async Task<ActionResult<List<AiHistoryItemDto>>> History() =>
        Ok(await assistant.HistoryAsync(User.RequireCurrentUser()));
}
