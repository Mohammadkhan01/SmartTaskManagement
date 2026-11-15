using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.DAL.Identity;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Configurations;

public sealed class NotificationConfiguration
    : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.RecipientUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(notification => notification.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(notification => notification.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(notification => notification.Message)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(notification => notification.RelatedEntityType)
            .HasMaxLength(50);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(notification => notification.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(notification => new
        {
            notification.RecipientUserId,
            notification.CreatedAtUtc
        });

        builder.HasIndex(notification => new
        {
            notification.RecipientUserId,
            notification.IsRead,
            notification.CreatedAtUtc
        });
    }
}
