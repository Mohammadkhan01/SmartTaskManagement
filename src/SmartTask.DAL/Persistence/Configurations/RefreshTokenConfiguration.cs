using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.DAL.Identity;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Configurations;

public class RefreshTokenConfiguration
    : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("varchar(128)");

        builder.Property(rt => rt.CreatedByIp)
            .HasMaxLength(45)
            .HasColumnType("varchar(45)");

        builder.Property(rt => rt.RevokedByIp)
            .HasMaxLength(45)
            .HasColumnType("varchar(45)");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(rt => rt.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique();

        builder.HasIndex(rt => new { rt.UserId, rt.ExpiresAtUtc });

        builder.HasIndex(rt => rt.ExpiresAtUtc);
    }
}
