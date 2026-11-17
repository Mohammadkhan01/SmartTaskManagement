
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.DAL.Identity;
using SmartTask.Domain.Entities;
using SmartTask.Common.Constants;

namespace SmartTask.DAL.Persistence.Configurations;

public class TaskItemConfiguration
    : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks", table =>
        {
            table.HasCheckConstraint(
                "CK_Tasks_Status",
                "[Status] IN (1, 2, 3, 4)");

            table.HasCheckConstraint(
                "CK_Tasks_Priority",
                "[Priority] IN (1, 2, 3, 4)");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(FieldLimits.TaskTitleMaxLength);

        builder.Property(t => t.Description)
            .HasMaxLength(FieldLimits.TaskDescriptionMaxLength);

        builder.Property(t => t.Status)
            .IsRequired();

        builder.Property(t => t.Priority)
            .IsRequired();

        builder.Property(t => t.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(t => t.AssignedToUserId)
            .HasMaxLength(450);

        // Project relationship is configured in ProjectConfiguration.

        // Task creator
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional task assignee
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query and filtering indexes
        builder.HasIndex(t => new { t.ProjectId, t.Status });

        builder.HasIndex(t => new { t.AssignedToUserId, t.Status });

        builder.HasIndex(t => t.DueDateUtc);

        builder.HasIndex(t => new { t.ProjectId, t.Priority });

        // Optimistic concurrency
        builder.Property(t => t.RowVersion)
            .IsRowVersion();

        // Exclude soft-deleted tasks from normal queries.
        builder.HasQueryFilter(t =>
    !t.IsDeleted && !t.Project.IsDeleted);
    }
}
