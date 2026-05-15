using System.Net;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using McpManager.FunctionalTests.Fixtures;
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
