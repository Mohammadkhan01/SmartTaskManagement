using SmartTask.Domain.Enums;

namespace SmartTask.BLL.DTOs.Projects;

public sealed class ProjectMemberResponse
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public ProjectMemberRole ProjectRole { get; set; }
}
