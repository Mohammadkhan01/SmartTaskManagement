using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTask.BLL.DTOs.TaskComments;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.API.Controllers;

[ApiController]
[Authorize]
public sealed class TaskCommentsController : ControllerBase
{
    private readonly ITaskCommentService _commentService;

    public TaskCommentsController(ITaskCommentService commentService)
    {
        _commentService = commentService;
    }

    [HttpGet("api/v1/tasks/{taskId:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskCommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaskCommentResponse>>> GetByTask(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var comments = await _commentService.GetByTaskIdAsync(
            taskId,
            userId,
            cancellationToken);

        if (comments is null)
            return NotFound();

        return Ok(comments);
    }

    [HttpPost("api/v1/tasks/{taskId:guid}/comments")]
    [ProducesResponseType(typeof(TaskCommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskCommentResponse>> Create(
        Guid taskId,
        [FromBody] CreateTaskCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var comment = await _commentService.CreateAsync(
            taskId,
            request,
            userId,
            cancellationToken);

        if (comment is null)
            return NotFound();

        return StatusCode(StatusCodes.Status201Created, comment);
    }

    [HttpPut("api/v1/task-comments/{commentId:guid}")]
    [ProducesResponseType(typeof(TaskCommentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskCommentResponse>> Update(
        Guid commentId,
        [FromBody] UpdateTaskCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var comment = await _commentService.UpdateAsync(
            commentId,
            request,
            userId,
            cancellationToken);

        if (comment is null)
            return NotFound();

        return Ok(comment);
    }

    [HttpDelete("api/v1/task-comments/{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var deleted = await _commentService.DeleteAsync(
            commentId,
            userId,
            cancellationToken);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
