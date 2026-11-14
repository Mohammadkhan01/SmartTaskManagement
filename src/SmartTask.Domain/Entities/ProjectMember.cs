using SmartTask.Domain.Common;
using SmartTask.Domain.Enums;

namespace SmartTask.Domain.Entities;

public class ProjectMember : BaseEntity
{
    public Guid ProjectId { get; set; }

    // References the ASP.NET Identity user ID.
    public string UserId { get; set; } = string.Empty;

    public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Member;

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
}