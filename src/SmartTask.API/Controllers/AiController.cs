using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartTask.BLL.DTOs.AI;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.API.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Ai")]
[Route("api/v1/ai")]
public sealed class AiController : ControllerBase
{
    private readonly IAIService _aiService;

    public AiController(IAIService aiService)
    {
        _aiService = aiService;
    }

    /// <summary>
    /// Returns a suggested rewrite of a task description. Nothing is saved;
    /// the client must let the user review and approve the suggestion.
    /// </summary>
    [HttpPost("enhance-task-description")]
    [ProducesResponseType(typeof(EnhanceTaskDescriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<EnhanceTaskDescriptionResponse>> EnhanceTaskDescription(
        [FromBody] EnhanceTaskDescriptionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _aiService.EnhanceTaskDescriptionAsync(
            request,
            cancellationToken);

        return Ok(response);
    }
}
