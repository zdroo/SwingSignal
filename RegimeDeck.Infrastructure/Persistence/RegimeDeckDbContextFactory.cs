using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RegimeDeck.Infrastructure.Persistence;

public class RegimeDeckDbContextFactory : IDesignTimeDbContextFactory<RegimeDeckDbContext>
{
    public RegimeDeckDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../RegimeDeck.Api"))
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<RegimeDeckDbContext>();
        optionsBuilder.UseSqlServer(config.GetConnectionString("SqlServer"));

        return new RegimeDeckDbContext(optionsBuilder.Options);
    }
}
