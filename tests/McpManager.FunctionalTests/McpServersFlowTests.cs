using System.Net;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using McpManager.FunctionalTests.Fixtures;
using McpManager.FunctionalTests.Support;
using Microsoft.Playwright;
using Xunit;

namespace McpManager.FunctionalTests;

[Collection("e2e")]
public class McpServersFlowTests
{
    private readonly E2eFixture _e2e;

    public McpServersFlowTests(E2eFixture e2e) => _e2e = e2e;

    /// <summary>
    /// Drives McpServersController end-to-end through the real Kestrel pipeline
    /// running in-process (so the coverage collector instruments it): a real
    /// browser renders the login, Index and Show pages over the socket, and the
    /// Create POST flow (MapDtoToServer + McpServerManager.Create + SyncTools +
    /// redirect) runs through the same live pipeline. These controller paths are
    /// the largest uncovered block and are unreachable from the in-memory
    /// integration tests — driving them here is what moves the Codecov number.
    /// </summary>
    [Fact]
    public async Task LoginCreateAndViewServer_ExercisesControllerThroughRealPipeline()
    {
        var page = await _e2e.NewPageAsync();

        // Real browser login (submits the AntiForgery field + persists the auth
        // cookie natively).
        await page.GotoAsync("/auth/login");
        await page.FillAsync("[name='Email']", "admin@mcpmanager.local");
        await page.FillAsync("[name='Password']", "123456");
        await page.ClickAsync("#loginBtn");
        await page.WaitForURLAsync(u => !u.Contains("/auth/login"));

        // Server-rendered controller Index over the real socket via the browser.
        var index = await page.GotoAsync("/mcpservers");
        index!.Status.Should().Be(200);

        // Create POST flow through the same live pipeline, via a cookie-jar
        // HttpClient that authenticates itself against the running socket.
        var name = $"e2e-{Guid.NewGuid():N}";
        var location = await CreateHttpServerAsync(name);
        location.Should().MatchRegex(@"/mcpservers/show/[0-9a-fA-F-]{36}");

        // Server-rendered Show detail page (largest uncovered controller
        // member) for the freshly created server, asserted in the browser.
        var show = await page.GotoAsync(location);
        show!.Status.Should().Be(200);
        (await page.ContentAsync()).Should().Contain(name);
    }

    /// <summary>
    /// Edit GET renders the Form populated by <c>MapServerToDto</c> — a large
    /// controller block (server→dto projection) only reachable through the real
    /// pipeline + redirect round-trip, not the in-memory integration tests.
    /// </summary>
    [Fact]
    public async Task EditServer_RendersFormPopulatedFromExistingServer()
    {
        var page = await _e2e.NewPageAsync();

        await page.GotoAsync("/auth/login");
        await page.FillAsync("[name='Email']", "admin@mcpmanager.local");
        await page.FillAsync("[name='Password']", "123456");
        await page.ClickAsync("#loginBtn");
        await page.WaitForURLAsync(u => !u.Contains("/auth/login"));

        var name = $"e2e-edit-{Guid.NewGuid():N}";
        var showLocation = await CreateHttpServerAsync(name);
        var id = Regex.Match(showLocation, "[0-9a-fA-F-]{36}").Value;

        // Edit GET -> repository.Get -> MapServerToDto -> View("Form", dto).
        var edit = await page.GotoAsync($"/mcpservers/edit/{id}");
        edit!.Status.Should().Be(200);

        // MapServerToDto must round-trip the persisted Name into the form input;
        // a regression in the projection renders a blank/wrong edit form.
        var nameValue = await page.InputValueAsync("[name='Name']");
        nameValue.Should().Be(name);
    }

    /// <summary>
    /// The highest-value e2e flow: a real browser drives the JS-powered create
    /// form (transport-partial reload + advanced-command toggle, all from the
    /// Vite bundle), registering a real stdio MCP upstream. This exercises
    /// McpServersController.Create + MapDtoToServer's Stdio branch + the LIVE
    /// SyncTools success path + Show-with-tools — the largest uncovered cluster,
    /// unreachable without both a browser (JS) and a real upstream.
    /// </summary>
    [Fact]
    public async Task CreateStdioServerThroughBrowser_SyncsLiveToolsAndShowsThem()
    {
        var page = await _e2e.NewPageAsync();

        await page.GotoAsync("/auth/login");
        await page.FillAsync("[name='Email']", "admin@mcpmanager.local");
        await page.FillAsync("[name='Password']", "123456");
        await page.ClickAsync("#loginBtn");
        await page.WaitForURLAsync(u => !u.Contains("/auth/login"));

        await page.GotoAsync("/mcpservers/create");
        var name = $"e2e-stdio-{Guid.NewGuid():N}";
        await page.FillAsync("[name='Name']", name);

        // Bundle JS reloads the transport partial on select, then the advanced
        // toggle reveals Command/ArgumentsText — Playwright auto-waits for each.
        await page.SelectOptionAsync("[name='TransportType']", "Stdio");
        await page.Locator(".stdio-advanced-toggle").WaitForAsync();
        await page.CheckAsync(".stdio-advanced-toggle");
        await page.Locator("[name='Command']").WaitForAsync();
        await page.FillAsync("[name='Command']", "dotnet");
        await page.FillAsync("[name='ArgumentsText']", TestStdioServerLocator.DllPath);

        await page.GetByRole(AriaRole.Button, new() { Name = "Create Server" }).ClickAsync();

        // Create -> MapDtoToServer (Stdio) -> McpServerManager.Create ->
        // SyncTools spawns the real stdio server -> redirect to Show, which
        // lists the discovered "echo" tool.
        await page.WaitForURLAsync(
            new Regex(@"/mcpservers/show/[0-9a-fA-F-]{36}$"),
            new PageWaitForURLOptions { Timeout = 30_000 }
        );
        (await page.ContentAsync())
            .Should()
            .Contain("echo", "SyncTools must discover the test stdio server's Echo tool");
    }

    private async Task<string> CreateHttpServerAsync(string name)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = false,
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(_e2e.BaseUrl) };
        var ct = TestContext.Current.CancellationToken;

        var loginToken = ExtractAntiforgery(
            await (await http.GetAsync("/auth/login", ct)).Content.ReadAsStringAsync(ct)
        );
        await http.PostAsync(
            "/auth/login",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["AntiForgery"] = loginToken,
                    ["Email"] = "admin@mcpmanager.local",
                    ["Password"] = "123456",
                }
            ),
            ct
        );

        var createToken = ExtractAntiforgery(
            await (await http.GetAsync("/mcpservers/create", ct)).Content.ReadAsStringAsync(ct)
        );
        var createResp = await http.PostAsync(
            "/mcpservers/create",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["AntiForgery"] = createToken,
                    ["Name"] = name,
                    ["TransportType"] = "Http",
                    ["Uri"] = "https://upstream.invalid/mcp",
                }
            ),
            ct
        );
        createResp.StatusCode.Should().Be(HttpStatusCode.Found);
        return createResp.Headers.Location!.ToString();
    }

    private static string ExtractAntiforgery(string html)
    {
        var m = Regex.Match(html, "name=\"AntiForgery\"[^>]*value=\"([^\"]+)\"");
        m.Success.Should().BeTrue("the rendered form must contain an AntiForgery token");
        return m.Groups[1].Value;
    }
}
