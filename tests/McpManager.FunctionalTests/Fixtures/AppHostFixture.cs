using System.Net;
using System.Net.Sockets;
using Xunit;

namespace McpManager.FunctionalTests.Fixtures;

/// <summary>
/// Runs the real Web.Portal production entry point (<c>Program</c>) in-process
/// on a background thread, bound to a dynamic loopback port via environment
/// configuration. The real socket lets Playwright drive a browser; running
/// in-process keeps the app under the MTP code-coverage collector so the
/// controller/view lines the e2e flow exercises count toward Codecov (an
/// out-of-process <c>dotnet run</c> would not). No WebApplicationFactory /
/// TestServer is involved, so there is no in-memory-server cast conflict.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private string _dbPath;
    private Thread _appThread;
    private Exception _startupFault;

    public string BaseUrl { get; private set; }

    public async ValueTask InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mcpmanager-e2e-{Guid.NewGuid():N}.db");
        var port = GetFreeLoopbackPort();
        BaseUrl = $"http://127.0.0.1:{port}";

        // CreateBuilder(args) reads these at startup. Development env keeps the
        // pipeline identical to the integration tests (skips HSTS); with an
        // http-only URL and no https port, UseHttpsRedirection no-ops.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__ApplicationConnection",
            $"Data Source={_dbPath}"
        );
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", BaseUrl);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        // Invoked via reflection, ApplicationName would default to the test
        // assembly, so MapStaticAssets() would look for the wrong manifest.
        // Web.Portal's static-assets manifest is copied into this test bin
        // (the default content root) as McpManager.Web.Portal.staticwebassets
        // .endpoints.json, so pinning the application name is sufficient.
        Environment.SetEnvironmentVariable("ASPNETCORE_APPLICATIONNAME", "McpManager.Web.Portal");

        var entryPoint =
            typeof(McpManager.Web.Portal.Program).Assembly.EntryPoint
            ?? throw new InvalidOperationException("Web.Portal assembly has no entry point.");

        _appThread = new Thread(() =>
        {
            try
            {
                // Top-level Program: void Main(string[]) -> app.Run() blocks here
                // for the life of the test process (background thread).
                entryPoint.Invoke(null, [Array.Empty<string>()]);
            }
            catch (Exception ex)
            {
                // Unwrap reflection wrapper so a real startup failure (bad
                // connection string, migration error, port in use) is visible
                // instead of presenting only as a readiness timeout.
                _startupFault =
                    (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            }
        })
        {
            IsBackground = true,
            Name = "McpManager-e2e-host",
        };
        _appThread.Start();

        await WaitForReadyAsync();
    }

    public ValueTask DisposeAsync()
    {
        // app.Run() owns the thread until the test process exits; the OS then
        // reclaims the socket. Best-effort delete the isolated db (may be held
        // by SQLite until exit — same tolerance as the integration fixture).
        if (!string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // ignored
            }
        }
        return ValueTask.CompletedTask;
    }

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task WaitForReadyAsync()
    {
        using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (_startupFault is not null)
            {
                throw new InvalidOperationException(
                    "Web.Portal entry point faulted during startup.",
                    _startupFault
                );
            }
            try
            {
                var resp = await http.GetAsync("/auth/login");
                if (resp.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Socket not up yet — the host is still migrating/starting.
            }
            await Task.Delay(250);
        }
        throw new TimeoutException(
            $"Web.Portal host did not become ready at {BaseUrl} within 60s. "
                + (_startupFault?.ToString() ?? "(no entry-point exception captured)")
        );
    }
}
