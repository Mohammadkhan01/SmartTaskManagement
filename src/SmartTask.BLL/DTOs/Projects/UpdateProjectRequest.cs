using SmartTask.Domain.Enums;

namespace SmartTask.BLL.DTOs.Projects;

public class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    // When omitted, the current project status is preserved.
    public ProjectStatus? Status { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
