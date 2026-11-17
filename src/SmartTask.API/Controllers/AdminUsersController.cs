using Microsoft.AspNetCore.Authorization;
using SmartTask.API.Authentication;
using Microsoft.AspNetCore.Mvc;
using SmartTask.BLL.DTOs.Admin;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Common.Constants;

namespace SmartTask.API.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/v1/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminUserListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminUserListResponse>> GetUsers(
        [FromQuery] AdminUserListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var response = await _adminUserService.GetUsersAsync(
            query,
            cancellationToken);

        return Ok(response);
    }

    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserResponse>> UpdateStatus(
        string id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var response = await _adminUserService.UpdateStatusAsync(
            id,
            request,
            actorUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpPut("{id}/role")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserResponse>> UpdateRole(
        string id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var response = await _adminUserService.UpdateRoleAsync(
            id,
            request,
            actorUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }
}
