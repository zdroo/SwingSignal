using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Configurations;

public class SectorRotationRowConfiguration : IEntityTypeConfiguration<SectorRotationRow>
{
    public void Configure(EntityTypeBuilder<SectorRotationRow> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Symbol).IsRequired().HasMaxLength(32);
        builder.Property(r => r.Sector).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Stance).HasMaxLength(32);

        // One cached row per sector ETF; the whole board is one indexed read
        builder.HasIndex(r => r.Symbol).IsUnique();
    }
}
