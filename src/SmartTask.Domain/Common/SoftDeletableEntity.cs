namespace SmartTask.Domain.Common;

public abstract class SoftDeletableEntity : BaseEntity
{
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    // SQL Server optimistic concurrency token.
    public byte[] RowVersion { get; set; } = [];
}