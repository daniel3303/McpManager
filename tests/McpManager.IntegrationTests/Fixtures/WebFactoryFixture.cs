using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Fixtures;

public class WebFactoryFixture
    : WebApplicationFactory<McpManager.Web.Portal.Program>,
        IAsyncLifetime
{
    private string _dbPath;

    public ValueTask InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mcpmanager-tests-{Guid.NewGuid():N}.db");
        // Touch the factory once to trigger the host build (which runs migrations).
        _ = Services;
        return ValueTask.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (!string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // Best-effort cleanup; SQLite may still hold the file briefly on some platforms.
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Override the SQLite connection string so each fixture gets a fresh, isolated database
        // that the app's startup migration step will populate.
        builder.UseSetting("ConnectionStrings:ApplicationConnection", $"Data Source={_dbPath}");

        builder.ConfigureServices(services =>
        {
            services.AddFakeLogging();
        });
    }
}
