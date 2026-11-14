using SmartTask.Domain.Common;

namespace SmartTask.Domain.Entities;

public class AuditLog : BaseEntity
{
    // Nullable because some system actions may not have a user.
    public string? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    // Optional serialized details. Do not store passwords or tokens.
    public string? Details { get; set; }

    public string? IpAddress { get; set; }
}
