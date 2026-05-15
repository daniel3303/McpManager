using Microsoft.Playwright;
using Xunit;

namespace McpManager.FunctionalTests.Fixtures;

/// <summary>
/// Single shared fixture for the e2e collection: starts the in-process app
/// host (real loopback socket, coverage-instrumented) and one Chromium
/// browser. Browsers are provisioned via the bundled Playwright CLI
/// (idempotent — already cached locally; downloaded once in CI).
/// </summary>
public sealed class E2eFixture : IAsyncLifetime
{
    private readonly AppHostFixture _host = new();
    private IPlaywright _playwright;
    private IBrowser _browser;

    public string BaseUrl => _host.BaseUrl;

    public async ValueTask InitializeAsync()
    {
        await _host.InitializeAsync();

        // Fail fast with an actionable message if the Vite bundle isn't being
        // served — JS-driven flows would otherwise present as confusing
        // Playwright timeouts. CI builds the frontend before `dotnet build`;
        // locally it's a prerequisite (the .NET static-assets manifest is
        // baked at build time, so a runtime rebuild can't fix it).
        using (var probe = new HttpClient { BaseAddress = new Uri(_host.BaseUrl) })
        {
            var bundle = await probe.GetAsync("/dist/bundle.js");
            if (!bundle.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    "/dist/bundle.js is not served (status "
                        + (int)bundle.StatusCode
                        + "). Build the frontend before the e2e tier:\n"
                        + "  cd src/McpManager.Web.Portal && npm ci && npm run build\n"
                        + "then rebuild the solution. CI does this automatically."
                );
            }
        }

        // Best-effort: provision the bundled chromium (CI path / cold cache).
        // Not fatal — locally we drive the installed system Chrome, so a flaky
        // browser-zip download must not break the suite.
        try
        {
            Microsoft.Playwright.Program.Main(["install", "chromium"]);
        }
        catch
        {
            // ignored — Channel="chrome" below needs no Playwright download
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await LaunchAsync();
    }

    private async Task<IBrowser> LaunchAsync()
    {
        // Prefer the system-installed Chrome (no Playwright browser download).
        // Fall back to the bundled chromium (CI installs it via the ci step).
        try
        {
            return await _playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true, Channel = "chrome" }
            );
        }
        catch (PlaywrightException)
        {
            return await _playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true }
            );
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }
        _playwright?.Dispose();
        await _host.DisposeAsync();
    }

    /// <summary>A fresh isolated browser context + page bound to the app.</summary>
    public async Task<IPage> NewPageAsync()
    {
        var context = await _browser.NewContextAsync(
            new BrowserNewContextOptions { BaseURL = BaseUrl }
        );
        return await context.NewPageAsync();
    }
}

[CollectionDefinition("e2e")]
public sealed class E2eCollection : ICollectionFixture<E2eFixture>;
