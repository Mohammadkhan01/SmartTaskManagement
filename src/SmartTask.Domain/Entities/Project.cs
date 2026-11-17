using SmartTask.Domain.Common;
using SmartTask.Domain.Enums;

namespace SmartTask.Domain.Entities;

public class Project : SoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

    // References the ASP.NET Identity user ID.
    public string CreatedByUserId { get; set; } = string.Empty;

    public ICollection<ProjectMember> Members { get; set; }
        = new List<ProjectMember>();

    public ICollection<TaskItem> Tasks { get; set; }
        = new List<TaskItem>();
}
