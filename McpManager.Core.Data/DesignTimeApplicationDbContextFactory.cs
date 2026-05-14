using McpManager.Core.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace McpManager.Core.Data;

public class DesignTimeApplicationDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(GetStartupProjectPath())
            .AddJsonFile("designsettings.json", optional: false)
            .AddJsonFile("designsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = configuration.GetConnectionString("ApplicationConnection");

        optionsBuilder.UseSqlite(
            connectionString,
            options =>
            {
                options.MigrationsAssembly(GetType().Assembly);
            }
        );

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string GetStartupProjectPath()
    {
        return Directory.GetCurrentDirectory();
    }
}
