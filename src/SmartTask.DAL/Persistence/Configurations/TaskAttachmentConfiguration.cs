using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.DAL.Identity;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Configurations;

public class TaskAttachmentConfiguration
    : IEntityTypeConfiguration<TaskAttachment>
{
    public void Configure(EntityTypeBuilder<TaskAttachment> builder)
    {
        builder.ToTable("TaskAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UploadedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(a => a.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.StoredFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(a => a.FileSize)
            .IsRequired();

        builder.Property(a => a.StoragePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(a => a.Task)
            .WithMany()
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TaskId, a.CreatedAtUtc });

        builder.HasIndex(a => new { a.UploadedByUserId, a.CreatedAtUtc });

        builder.Property(a => a.RowVersion)
            .IsRowVersion();

        builder.HasQueryFilter(a =>
            !a.IsDeleted && !a.Task.IsDeleted && !a.Task.Project.IsDeleted);
    }
}
