namespace SmartTask.BLL.DTOs.Dashboard;

public sealed class DashboardTaskStatisticsResponse
{
    public int TotalTasks { get; set; }

    public int ToDoTasks { get; set; }

    public int InProgressTasks { get; set; }

    public int CompletedTasks { get; set; }

    public int CancelledTasks { get; set; }

    public int PendingTasks { get; set; }

    public decimal CompletionPercentage { get; set; }

    public int LowPriorityTasks { get; set; }

    public int MediumPriorityTasks { get; set; }

    public int HighPriorityTasks { get; set; }

    public int CriticalPriorityTasks { get; set; }

    public IReadOnlyList<DashboardTaskItemResponse> UpcomingTasks { get; set; }
        = [];
}
