namespace SmartTask.Domain.Entities;

public class AuditLog
{
    // BIGINT IDENTITY assigned by the database.
    public long Id { get; set; }

    // Nullable because some system actions may not have a user.
    public string? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    // Optional serialized details. Do not store passwords or tokens.
    public string? Details { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public string? IpAddress { get; set; }
}
