using SmartTask.Domain.Enums;

namespace SmartTask.BLL.DTOs.Projects;

public sealed class ProjectListQueryRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public ProjectStatus? Status { get; set; }
    public string SortBy { get; set; } = "createdAt";
    public bool SortDescending { get; set; } = true;
}
