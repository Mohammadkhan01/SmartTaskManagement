using Microsoft.AspNetCore.Authorization;
using SmartTask.API.Authentication;
using Microsoft.AspNetCore.Mvc;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("task-statistics")]
    public async Task<IActionResult> GetMyTaskStatistics(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var statistics = await _dashboardService.GetMyTaskStatisticsAsync(
            userId,
            cancellationToken);

        return Ok(statistics);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetMyDashboard(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var dashboard = await _dashboardService.GetMyDashboardAsync(
            userId,
            cancellationToken);

        return Ok(dashboard);
    }

    [HttpGet("projects/{projectId:guid}")]
    public async Task<IActionResult> GetProjectDashboard(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var dashboard = await _dashboardService.GetProjectDashboardAsync(
            projectId,
            userId,
            cancellationToken);

        return dashboard is null ? NotFound() : Ok(dashboard);
    }

    [HttpGet("projects/{projectId:guid}/workload")]
    public async Task<IActionResult> GetProjectWorkload(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var workload = await _dashboardService.GetProjectWorkloadAsync(
            projectId,
            userId,
            cancellationToken);

        return workload is null ? NotFound() : Ok(workload);
    }
}
