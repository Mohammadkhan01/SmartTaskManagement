using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.DAL.Identity;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects", table =>
            table.HasCheckConstraint(
                "CK_Projects_Status",
                "[Status] IN (1, 2, 3, 4, 5)"));

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(p => p.Status)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Members)
            .WithOne(pm => pm.Project)
            .HasForeignKey(pm => pm.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Tasks)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.CreatedByUserId, p.Status });
        builder.HasIndex(p => new { p.Status, p.CreatedAtUtc });

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
