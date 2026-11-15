using SmartTask.BLL.DTOs.Dashboard;

namespace SmartTask.BLL.Interfaces.Services;

public interface IDashboardService
{
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
}
