using SmartTask.Domain.Enums;

namespace SmartTask.BLL.DTOs.Projects;

public class ProjectResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ProjectStatus Status { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
