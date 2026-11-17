using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTask.BLL.DTOs.TaskComments;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tasks/{taskId:guid}/activity")]
public sealed class TaskActivitiesController : ControllerBase
{
    private readonly ITaskActivityService _taskActivityService;

    public TaskActivitiesController(
        ITaskActivityService taskActivityService)
    {
        _taskActivityService = taskActivityService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<TaskActivityResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaskActivityResponse>>>
        GetByTaskId(
            Guid taskId,
            CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var activities = await _taskActivityService.GetByTaskIdAsync(
            taskId,
            userId,
            cancellationToken);

        if (activities is null)
            return NotFound();

        return Ok(activities);
    }
}
