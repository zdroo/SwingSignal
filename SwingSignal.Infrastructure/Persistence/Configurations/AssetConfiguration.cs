using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Symbol).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(100);
        builder.Property(a => a.MarketType).IsRequired();

        builder.HasIndex(a => a.Symbol).IsUnique();

        builder.HasMany(a => a.Candles)
            .WithOne(c => c.Asset)
            .HasForeignKey(c => c.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
