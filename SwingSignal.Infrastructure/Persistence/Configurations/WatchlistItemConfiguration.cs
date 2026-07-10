using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure.Persistence.Configurations;

public class WatchlistItemConfiguration : IEntityTypeConfiguration<WatchlistItem>
{
    public void Configure(EntityTypeBuilder<WatchlistItem> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Symbol).IsRequired().HasMaxLength(32);
        builder.Property(w => w.Name).IsRequired().HasMaxLength(128);

        // One row per user+symbol; the user's whole list is one indexed read
        builder.HasIndex(w => new { w.UserId, w.Symbol }).IsUnique();

        // GDPR account deletion takes the watchlist with it
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
