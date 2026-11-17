using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTask.BLL.DTOs.Dashboard;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Common.Constants;

namespace SmartTask.API.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/v1/admin/dashboard")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public AdminDashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminDashboardResponse>> GetAdminDashboard(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var dashboard = await _dashboardService.GetAdminDashboardAsync(
            userId,
            cancellationToken);

        return dashboard is null ? Forbid() : Ok(dashboard);
    }
}
