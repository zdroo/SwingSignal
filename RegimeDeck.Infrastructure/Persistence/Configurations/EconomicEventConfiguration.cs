using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Configurations;

public class EconomicEventConfiguration : IEntityTypeConfiguration<EconomicEvent>
{
    public void Configure(EntityTypeBuilder<EconomicEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(96);
        builder.Property(e => e.Impact).IsRequired().HasMaxLength(16);

        // One row per release occurrence; "upcoming" queries hit the date index
        builder.HasIndex(e => new { e.ReleaseId, e.Date }).IsUnique();
        builder.HasIndex(e => e.Date);
    }
}
