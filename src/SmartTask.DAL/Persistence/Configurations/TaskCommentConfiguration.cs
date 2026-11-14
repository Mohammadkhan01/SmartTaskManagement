using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.DAL.Identity;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Configurations;

public class TaskCommentConfiguration
    : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("TaskComments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(5000);

        builder.HasOne(c => c.Task)
            .WithMany()
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.TaskId, c.CreatedAtUtc });

        builder.HasIndex(c => new { c.UserId, c.CreatedAtUtc });

        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        builder.HasQueryFilter(c =>
            !c.IsDeleted && !c.Task.IsDeleted);
    }
}
