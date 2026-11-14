
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
        builder.ToTable("ProjectMembers");

        builder.HasKey(pm => pm.Id);
        builder.HasQueryFilter(pm => !pm.Project.IsDeleted);
        builder.Property(pm => pm.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(pm => pm.Role)
            .IsRequired();

        builder.Property(pm => pm.JoinedAtUtc)
            .IsRequired();

        // Project relationship is configured in ProjectConfiguration.

        // User relationship
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(pm => pm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // A user can join a project only once.
        builder.HasIndex(pm => new { pm.ProjectId, pm.UserId })
            .IsUnique();

        // Find all projects belonging to a user.
        builder.HasIndex(pm => pm.UserId);


    }
}
