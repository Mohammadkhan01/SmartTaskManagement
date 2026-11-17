using SmartTask.Domain.Enums;

namespace SmartTask.Domain.Entities;

public class ProjectMember
{
    public Guid ProjectId { get; set; }

    // References the ASP.NET Identity user ID.
    public string UserId { get; set; } = string.Empty;

    public ProjectMemberRole ProjectRole { get; set; } = ProjectMemberRole.Member;

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
}
