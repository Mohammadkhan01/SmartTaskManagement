using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.DAL.Identity;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Configurations;

public class ProjectMemberConfiguration
    : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers", table =>
            table.HasCheckConstraint(
                "CK_ProjectMembers_ProjectRole",
                "[ProjectRole] IN (1, 2)"));

        builder.HasKey(pm => new { pm.ProjectId, pm.UserId });

        builder.HasQueryFilter(pm => !pm.Project.IsDeleted);

        builder.Property(pm => pm.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(pm => pm.ProjectRole)
            .HasColumnName("ProjectRole")
            .IsRequired();

        builder.Property(pm => pm.JoinedAtUtc)
            .IsRequired();

        // Project relationship is configured in ProjectConfiguration.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(pm => pm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supports finding all projects for a user.
        builder.HasIndex(pm => new { pm.UserId, pm.ProjectId });
    }
}
