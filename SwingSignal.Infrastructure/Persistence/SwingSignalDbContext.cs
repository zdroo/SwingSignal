using Microsoft.EntityFrameworkCore;
using SwingSignal.Domain.Entities;

namespace SwingSignal.Infrastructure.Persistence;

public class SwingSignalDbContext : DbContext
{
    public SwingSignalDbContext(DbContextOptions<SwingSignalDbContext> options) : base(options) { }

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<MacroDataPoint> MacroDataPoints => Set<MacroDataPoint>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SwingSignalDbContext).Assembly);
    }
}
