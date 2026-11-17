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

        builder.Property(a => a.Id)
            .UseIdentityColumn();

        builder.Property(a => a.UserId)
            .HasMaxLength(450);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .HasMaxLength(100);

        builder.Property(a => a.Details)
            .HasMaxLength(2000);

        builder.Property(a => a.TimestampUtc)
            .IsRequired();

        builder.Property(a => a.IpAddress)
            .IsUnicode(false)
            .HasMaxLength(45);

        // User relationship is optional because system events
        // may not be associated with an authenticated user.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.UserId, a.TimestampUtc });

        // Supports the task activity timeline lookup.
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
    }
}
