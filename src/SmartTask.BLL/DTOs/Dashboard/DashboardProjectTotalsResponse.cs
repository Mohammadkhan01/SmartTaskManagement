namespace SmartTask.BLL.DTOs.Dashboard;

public sealed class DashboardProjectTotalsResponse
{
    public int TotalProjects { get; set; }
    public int PlanningProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int OnHoldProjects { get; set; }
    public int ArchivedProjects { get; set; }
}
