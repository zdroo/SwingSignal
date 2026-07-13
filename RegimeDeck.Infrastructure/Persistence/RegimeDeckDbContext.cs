using Microsoft.EntityFrameworkCore;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Persistence;

public class RegimeDeckDbContext : DbContext
{
    public RegimeDeckDbContext(DbContextOptions<RegimeDeckDbContext> options) : base(options) { }

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<MacroDataPoint> MacroDataPoints => Set<MacroDataPoint>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SearchLog> SearchLogs => Set<SearchLog>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<AlertState> AlertStates => Set<AlertState>();
    public DbSet<ScreenerRow> ScreenerRows => Set<ScreenerRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RegimeDeckDbContext).Assembly);
    }
}
