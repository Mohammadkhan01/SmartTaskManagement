using SmartTask.BLL.DTOs.Dashboard;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Domain.Enums;

namespace SmartTask.BLL.Services.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private const int RecentTaskCount = 10;

    private readonly IDashboardRepository _dashboardRepository;
    private readonly IProjectMemberRepository _projectMemberRepository;

    public DashboardService(
        IDashboardRepository dashboardRepository,
        IProjectMemberRepository projectMemberRepository)
    {
        _dashboardRepository = dashboardRepository;
        _projectMemberRepository = projectMemberRepository;
    }

    public async Task<DashboardSummaryResponse> GetMyDashboardAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var nowUtc = DateTime.UtcNow;
        var startOfTodayUtc = nowUtc.Date;
        var startOfTomorrowUtc = startOfTodayUtc.AddDays(1);

        return await _dashboardRepository.GetMyDashboardAsync(
            userId,
            nowUtc,
            startOfTodayUtc,
            startOfTomorrowUtc,
            RecentTaskCount,
            cancellationToken);
    }

    public async Task<DashboardSummaryResponse?> GetProjectDashboardAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var membership = await _projectMemberRepository
            .GetByProjectAndUserAsync(
                projectId,
                userId,
                cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var nowUtc = DateTime.UtcNow;
        var startOfTodayUtc = nowUtc.Date;
        var startOfTomorrowUtc = startOfTodayUtc.AddDays(1);

        return await _dashboardRepository.GetProjectDashboardAsync(
            projectId,
            nowUtc,
            startOfTodayUtc,
            startOfTomorrowUtc,
            RecentTaskCount,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MemberWorkloadResponse>?>
        GetProjectWorkloadAsync(
            Guid projectId,
            string userId,
            CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var membership = await _projectMemberRepository
            .GetByProjectAndUserAsync(
                projectId,
                userId,
                cancellationToken);

        if (membership is null ||
            membership.Role != ProjectMemberRole.Manager)
        {
            return null;
        }

        return await _dashboardRepository.GetProjectWorkloadAsync(
            projectId,
            DateTime.UtcNow,
            cancellationToken);
    }
}
