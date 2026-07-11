using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure.Persistence.Configurations;

public class AlertStateConfiguration : IEntityTypeConfiguration<AlertState>
{
    public void Configure(EntityTypeBuilder<AlertState> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Key).IsRequired().HasMaxLength(64);
        builder.Property(a => a.Value).IsRequired().HasMaxLength(64);
        builder.HasIndex(a => a.Key).IsUnique();
    }
}
