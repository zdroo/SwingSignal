using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure.Persistence.Configurations;

public class CandleConfiguration : IEntityTypeConfiguration<Candle>
{
    public void Configure(EntityTypeBuilder<Candle> builder)
    {
        builder.ToTable("Candles");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Open).HasPrecision(18, 8);
        builder.Property(c => c.High).HasPrecision(18, 8);
        builder.Property(c => c.Low).HasPrecision(18, 8);
        builder.Property(c => c.Close).HasPrecision(18, 8);
        builder.Property(c => c.Volume).HasPrecision(24, 8);

        // Prevent duplicate candles for same asset+interval+time
        builder.HasIndex(c => new { c.AssetId, c.Interval, c.OpenTime }).IsUnique();
    }
}
