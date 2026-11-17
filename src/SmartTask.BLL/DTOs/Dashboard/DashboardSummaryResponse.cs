namespace SmartTask.BLL.DTOs.Dashboard;

public sealed class DashboardSummaryResponse
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public int TotalTasks { get; set; }
    public int ToDoTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int CancelledTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int DueTodayTasks { get; set; }

    public decimal CompletionPercentage { get; set; }

    // Populated for the personal dashboard only.
    public DashboardProjectTotalsResponse? ProjectTotals { get; set; }

    public IReadOnlyList<DashboardTaskItemResponse> RecentTasks { get; set; }
        = [];
}
