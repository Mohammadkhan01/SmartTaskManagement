namespace SmartTask.Common.Constants;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string ProjectManager = "Project Manager";
    public const string TeamMember = "Team Member";

    public static IReadOnlyList<string> All { get; } =
    [
        Admin,
        ProjectManager,
        TeamMember
    ];
}
