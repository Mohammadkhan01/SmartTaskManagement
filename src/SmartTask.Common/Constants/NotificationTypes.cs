namespace SmartTask.Common.Constants;

// Persisted in Notifications.Type and returned to clients.
public static class NotificationTypes
{
    public const string TaskAssigned = "TaskAssigned";
    public const string TaskReassigned = "TaskReassigned";
    public const string TaskStatusChanged = "TaskStatusChanged";
    public const string TaskCommentAdded = "TaskCommentAdded";
}
