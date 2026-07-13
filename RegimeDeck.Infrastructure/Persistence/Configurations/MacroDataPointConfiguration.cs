using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Configurations;

public class MacroDataPointConfiguration : IEntityTypeConfiguration<MacroDataPoint>
{
    public void Configure(EntityTypeBuilder<MacroDataPoint> builder)
    {
        builder.ToTable("MacroDataPoints");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Value).HasPrecision(18, 6);
        builder.Property(m => m.Source).IsRequired().HasMaxLength(50);

        builder.HasIndex(m => new { m.IndicatorType, m.Date }).IsUnique();
    }
}
