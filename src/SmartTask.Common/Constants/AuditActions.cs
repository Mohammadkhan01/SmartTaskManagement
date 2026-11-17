namespace SmartTask.Common.Constants;

// Persisted in AuditLogs.Action and returned by the task activity endpoint; changing a value breaks history.
public static class AuditActions
{
    public const string ProjectCreated = "ProjectCreated";
    public const string ProjectUpdated = "ProjectUpdated";
    public const string ProjectStatusChanged = "ProjectStatusChanged";
    public const string ProjectDeleted = "ProjectDeleted";
    public const string ProjectMemberAdded = "ProjectMemberAdded";
    public const string ProjectMemberRemoved = "ProjectMemberRemoved";

    public const string TaskCreated = "TaskCreated";
    public const string TaskUpdated = "TaskUpdated";
    public const string TaskAssigned = "TaskAssigned";
    public const string TaskStatusChanged = "TaskStatusChanged";
    public const string TaskDeleted = "TaskDeleted";

    public const string CommentCreated = "CommentCreated";
    public const string CommentUpdated = "CommentUpdated";
    public const string CommentDeleted = "CommentDeleted";

    public const string AttachmentUploaded = "AttachmentUploaded";
    public const string AttachmentDeleted = "AttachmentDeleted";

    public const string UserStatusChanged = "UserStatusChanged";
    public const string UserRoleChanged = "UserRoleChanged";
}
