using AngleSharp;
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

    /// <summary>
    /// Signs the seeded admin (admin@mcpmanager.local / 123456) in on the
    /// provided client, fetching the antiforgery token from the rendered
    /// login form first. The client must have HandleCookies enabled so the
    /// antiforgery cookie and the resulting auth cookie are preserved.
    /// </summary>
    public async Task<HttpResponseMessage> SignInAsAdminAsync(
        HttpClient client,
        CancellationToken ct = default
    )
    {
        var getResponse = await client.GetAsync("/Auth/Login", ct);
        getResponse.EnsureSuccessStatusCode();
        var html = await getResponse.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);

        // Program.cs configures AddAntiforgery(opts => opts.FormFieldName = "AntiForgery").
        var antiForgeryToken = document
            .QuerySelector("form#loginForm input[name='AntiForgery']")!
            .GetAttribute("value")!;

        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = antiForgeryToken,
                ["Email"] = "admin@mcpmanager.local",
                ["Password"] = "123456",
            }
        );

        return await client.PostAsync("/Auth/Login", form, ct);
    }
}
