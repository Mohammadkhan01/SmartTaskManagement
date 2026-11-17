namespace SmartTask.BLL.DTOs.Dashboard;

public sealed class AdminDashboardResponse
{
    public DashboardProjectTotalsResponse Projects { get; set; } = new();

    public int TotalTasks { get; set; }
    public int ToDoTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int CancelledTasks { get; set; }
    public int OverdueTasks { get; set; }
    public decimal CompletionPercentage { get; set; }

    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int AdminUsers { get; set; }
    public int ProjectManagerUsers { get; set; }
    public int TeamMemberUsers { get; set; }
}
