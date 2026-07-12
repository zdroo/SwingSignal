using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence.Configurations;

public class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Email).IsRequired().HasMaxLength(256);
        builder.Property(w => w.Source).HasMaxLength(64);
        builder.HasIndex(w => w.Email).IsUnique();
    }
}
