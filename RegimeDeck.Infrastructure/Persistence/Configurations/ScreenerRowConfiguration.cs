using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Configurations;

public class ScreenerRowConfiguration : IEntityTypeConfiguration<ScreenerRow>
{
    public void Configure(EntityTypeBuilder<ScreenerRow> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Symbol).IsRequired().HasMaxLength(32);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(128);
        builder.Property(r => r.Stance).HasMaxLength(32);
        builder.Property(r => r.Strength).HasMaxLength(32);
        builder.Property(r => r.CurrentPrice).HasPrecision(18, 8); // match Candle prices

        // One cached row per symbol; the whole board is one indexed read
        builder.HasIndex(r => r.Symbol).IsUnique();
    }
}
