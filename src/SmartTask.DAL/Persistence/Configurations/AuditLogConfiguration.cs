

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.DAL.Identity;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Configurations;

public class AuditLogConfiguration
    : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId)
            .HasMaxLength(450);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .HasMaxLength(450);

        builder.Property(a => a.Details)
            .HasMaxLength(4000);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        // User relationship is optional because system events
        // may not be associated with an authenticated user.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Efficient audit-history queries.
        builder.HasIndex(a => new { a.UserId, a.CreatedAtUtc });

        builder.HasIndex(a => a.CreatedAtUtc);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
