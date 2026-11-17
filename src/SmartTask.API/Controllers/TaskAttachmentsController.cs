using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTask.BLL.DTOs.TaskAttachments;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.API.Controllers;

[ApiController]
[Authorize]
public sealed class TaskAttachmentsController : ControllerBase
{
    private const long MaxRequestSize = 11 * 1024 * 1024;

    private readonly ITaskAttachmentService _attachmentService;

    public TaskAttachmentsController(
        ITaskAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpPost("api/v1/tasks/{taskId:guid}/attachments")]
    [RequestSizeLimit(MaxRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
    [ProducesResponseType(typeof(TaskAttachmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskAttachmentResponse>> Upload(
        Guid taskId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest("A non-empty file is required.");

        await using var stream = file.OpenReadStream();

        var attachment = await _attachmentService.UploadAsync(
            taskId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            userId,
            cancellationToken);

        if (attachment is null)
            return NotFound();

        return StatusCode(
            StatusCodes.Status201Created,
            attachment);
    }

    [HttpGet("api/v1/tasks/{taskId:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskAttachmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaskAttachmentResponse>>> GetByTask(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var attachments = await _attachmentService.GetByTaskIdAsync(
            taskId,
            userId,
            cancellationToken);

        if (attachments is null)
            return NotFound();

        return Ok(attachments);
    }

    [HttpGet("api/v1/task-attachments/{attachmentId:guid}")]
    [ProducesResponseType(typeof(TaskAttachmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskAttachmentResponse>> GetById(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var attachment = await _attachmentService.GetByIdAsync(
            attachmentId,
            userId,
            cancellationToken);

        if (attachment is null)
            return NotFound();

        return Ok(attachment);
    }


[HttpGet("api/v1/task-attachments/{attachmentId:guid}/download")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Download(
    Guid attachmentId,
    CancellationToken cancellationToken)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (string.IsNullOrWhiteSpace(userId))
        return Unauthorized();

    var download = await _attachmentService.OpenDownloadAsync(
        attachmentId,
        userId,
        cancellationToken);

    if (download is null)
        return NotFound();

    return File(
        download.Stream,
        download.ContentType,
        download.OriginalFileName);
}



    [HttpDelete("api/v1/task-attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var deleted = await _attachmentService.DeleteAsync(
            attachmentId,
            userId,
            cancellationToken);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
