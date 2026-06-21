using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SwingSignal.Infrastructure;

public class SwingSignalDbContextFactory : IDesignTimeDbContextFactory<SwingSignalDbContext>
{
    public SwingSignalDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../SwingSignal.Api"))
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SwingSignalDbContext>();
        optionsBuilder.UseSqlServer(config.GetConnectionString("SqlServer"));

        return new SwingSignalDbContext(optionsBuilder.Options);
    }
}
