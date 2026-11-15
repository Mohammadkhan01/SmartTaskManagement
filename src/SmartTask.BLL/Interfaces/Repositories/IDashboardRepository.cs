using SmartTask.BLL.DTOs.Dashboard;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface IDashboardRepository
{
    Task<DashboardSummaryResponse> GetMyDashboardAsync(
        string userId,
        DateTime nowUtc,
        DateTime startOfTodayUtc,
        DateTime startOfTomorrowUtc,
        int recentTaskCount,
        CancellationToken cancellationToken = default);

    Task<DashboardSummaryResponse> GetProjectDashboardAsync(
        Guid projectId,
        DateTime nowUtc,
        DateTime startOfTodayUtc,
        DateTime startOfTomorrowUtc,
        int recentTaskCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberWorkloadResponse>> GetProjectWorkloadAsync(
        Guid projectId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
