using SmartTask.BLL.DTOs.Dashboard;

namespace SmartTask.BLL.Interfaces.Services;

public interface IDashboardService
{
    Task<DashboardTaskStatisticsResponse> GetMyTaskStatisticsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<DashboardSummaryResponse> GetMyDashboardAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<DashboardSummaryResponse?> GetProjectDashboardAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberWorkloadResponse>?> GetProjectWorkloadAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns system-wide totals, or null when the user is not an Admin.
    /// </summary>
    Task<AdminDashboardResponse?> GetAdminDashboardAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
