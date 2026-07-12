using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Configurations;

public class SearchLogConfiguration : IEntityTypeConfiguration<SearchLog>
{
    public void Configure(EntityTypeBuilder<SearchLog> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Symbol).IsRequired().HasMaxLength(32);
        builder.Property(s => s.RawQuery).HasMaxLength(200);
        builder.Property(s => s.Source).HasMaxLength(32);

        // "Most searched in the last N days" queries
        builder.HasIndex(s => new { s.CreatedAt, s.Symbol });
    }
}
