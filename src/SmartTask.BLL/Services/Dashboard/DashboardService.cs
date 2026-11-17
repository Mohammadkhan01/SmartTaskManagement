using SmartTask.BLL.DTOs.Dashboard;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Domain.Enums;
using SmartTask.Common.Constants;

namespace SmartTask.BLL.Services.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private const int RecentTaskCount = 10;

    private readonly IDashboardRepository _dashboardRepository;
    private readonly IProjectMemberRepository _projectMemberRepository;
    private readonly IUserAccountRepository _userAccountRepository;

    public DashboardService(
        IDashboardRepository dashboardRepository,
        IProjectMemberRepository projectMemberRepository,
        IUserAccountRepository userAccountRepository)
    {
        _dashboardRepository = dashboardRepository;
        _projectMemberRepository = projectMemberRepository;
        _userAccountRepository = userAccountRepository;
    }

    private const int UpcomingTaskCount = 10;
    private const int UpcomingWindowDays = 7;

    public async Task<DashboardTaskStatisticsResponse> GetMyTaskStatisticsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var nowUtc = DateTime.UtcNow;
        var upcomingCutoffUtc = nowUtc.AddDays(UpcomingWindowDays);

        return await _dashboardRepository.GetMyTaskStatisticsAsync(
            userId,
            nowUtc,
            upcomingCutoffUtc,
            UpcomingTaskCount,
            cancellationToken);
    }

    public async Task<DashboardSummaryResponse> GetMyDashboardAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var nowUtc = DateTime.UtcNow;
        var startOfTodayUtc = nowUtc.Date;
        var startOfTomorrowUtc = startOfTodayUtc.AddDays(1);

        var summary = await _dashboardRepository.GetMyDashboardAsync(
            userId,
            nowUtc,
            startOfTodayUtc,
            startOfTomorrowUtc,
            RecentTaskCount,
            cancellationToken);

        summary.ProjectTotals =
            await _dashboardRepository.GetMemberProjectTotalsAsync(
                userId,
                cancellationToken);

        return summary;
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
            membership.ProjectRole != ProjectMemberRole.Manager)
        {
            return null;
        }

        return await _dashboardRepository.GetProjectWorkloadAsync(
            projectId,
            DateTime.UtcNow,
            cancellationToken);
    }

    public async Task<AdminDashboardResponse?> GetAdminDashboardAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var roles = await _userAccountRepository.GetRolesAsync(
            userId,
            cancellationToken);

        if (!roles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return await _dashboardRepository.GetAdminDashboardAsync(
            DateTime.UtcNow,
            cancellationToken);
    }
}
