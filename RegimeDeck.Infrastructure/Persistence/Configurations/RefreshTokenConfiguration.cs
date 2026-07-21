using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(t => t.Id);

        // Lookups are always by hash; it uniquely identifies a token
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Revoking a whole family (logout / reuse detection) queries by FamilyId
        builder.HasIndex(t => t.FamilyId);

        // Revoke-all-for-user (password change, logout everywhere) queries by UserId
        builder.HasIndex(t => t.UserId);

        builder.Property(t => t.UserAgent).HasMaxLength(256);

        // Deleting the account takes its sessions with it
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
