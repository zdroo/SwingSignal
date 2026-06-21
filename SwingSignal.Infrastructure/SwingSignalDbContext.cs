using Microsoft.EntityFrameworkCore;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure;

public class SwingSignalDbContext : DbContext
{
    public SwingSignalDbContext(DbContextOptions<SwingSignalDbContext> options) : base(options) { }

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<MacroDataPoint> MacroDataPoints => Set<MacroDataPoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SwingSignalDbContext).Assembly);
    }
}
