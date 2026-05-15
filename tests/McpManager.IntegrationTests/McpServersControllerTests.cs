using System.Net;
using System.Text.Json;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetShow_WithUnknownId_RedirectsToIndex()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        // Random guid that cannot exist in the seeded DB — exercises the
        // not-found branch. A regression that returns 500 (unhandled null) or
        // 404 (action gone) instead of redirecting surfaces here.
        var response = await client.GetAsync($"/McpServers/Show/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWithEquivalentOf("/mcpservers");
    }

    [Fact]
    public async Task PostDelete_WithUnknownId_RedirectsToIndex()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var token = await HarvestAntiforgeryAsync(client, "/McpServers", ct);
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        // Not-found branch on Delete: server == null -> flash error + redirect.
        // Distinct from GET Show's not-found branch because this is a POST with
        // antiforgery, exercising the route binding + token validation as well.
        var response = await client.PostAsync($"/McpServers/Delete/{Guid.NewGuid()}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWithEquivalentOf("/mcpservers");
    }

    [Fact]
    public async Task PostCreate_WithEmptyName_ReturnsTwoHundredFormReRender()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var token = await HarvestAntiforgeryAsync(client, "/McpServers/Create", ct);
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "",
                ["TransportType"] = "Http",
                ["Uri"] = "https://example.invalid/",
            }
        );

        // ModelState fails on the [Required] Name -> action returns View("Form", dto).
        // A regression that promoted ModelState errors past the gate (and into
        // McpServerManager.Create with an empty name) would surface as a 5xx or
        // an unintended persist.
        var response = await client.PostAsync("/McpServers/Create", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostAddHeader_AppendsRowAndReturnsCustomHeadersPartialView()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var token = await HarvestAntiforgeryAsync(client, "/McpServers/Create", ct);
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "any",
                ["CustomHeaders[0].Key"] = "X-Test",
                ["CustomHeaders[0].Value"] = "1",
            }
        );

        // AddHeader is a pure view-manipulation endpoint (no DB) that returns
        // PartialView("_CustomHeadersForm", dto) with one extra empty row. A
        // regression that breaks the partial-view rendering or the route would
        // surface as a 500 or a missing input on the returned HTML.
        var response = await client.PostAsync("/McpServers/AddHeader", form, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var keyInputs = document.QuerySelectorAll("input[name$='.Key']");
        keyInputs.Length.Should().BeGreaterThan(1, "AddHeader must append a new row to the form");
    }

    [Fact]
    public async Task GetEdit_WithExistingId_RendersFormPrefilledWithServerName()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var server = await SeedHttpServerAsync($"edit-{Guid.NewGuid():N}");

        // Edit GET on an existing server is the only path that runs
        // MapServerToDto and the Form.cshtml with a pre-populated model —
        // a regression in the mapping or in any asp-for on Form.cshtml
        // would render the value blank or 500. AngleSharp asserts on the
        // rendered Name input.
        var response = await client.GetAsync($"/McpServers/Edit/{server.Id}", ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        document
            .QuerySelector("input[name='Name']")!
            .GetAttribute("value")
            .Should()
            .Be(server.Name);
    }

    [Fact]
    public async Task PostDelete_WithExistingId_RemovesServerAndRedirectsToIndex()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var server = await SeedHttpServerAsync($"to-delete-{Guid.NewGuid():N}");
        var token = await HarvestAntiforgeryAsync(client, "/McpServers", ct);
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        // Delete happy path: server exists -> McpServerManager.Delete
        // (SaveChanges + log) -> 302 to /mcpservers. Asserts both the
        // redirect and that the row is gone via a fresh repository read,
        // covering the row-deletion contract the UI depends on.
        var response = await client.PostAsync($"/McpServers/Delete/{server.Id}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWithEquivalentOf("/mcpservers");

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var remaining = await repo.GetAll().AnyAsync(s => s.Id == server.Id, ct);
        remaining.Should().BeFalse("Delete must remove the row");
    }

    [Fact]
    public async Task PostPreviewCommand_NpxMode_ReturnsBuiltNpxCommandPreviewJson()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var token = await HarvestAntiforgeryAsync(client, "/McpServers/Create", ct);
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["UseAdvancedCommand"] = "false",
                ["NpmPackage"] = "@modelcontextprotocol/server-filesystem",
            }
        );

        // PreviewCommand is a pure JSON endpoint that picks the npx branch when
        // UseAdvancedCommand=false: BuildNpxCommand -> ("npx", ["-y", pkg]) ->
        // BuildCommandPreview joins with spaces. Nothing else exercises this
        // action; a regression that flips the ternary (advanced vs npx) or
        // drops the "-y" prefix surfaces directly in the returned preview.
        var response = await client.PostAsync("/McpServers/PreviewCommand", form, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("preview")
            .GetString()
            .Should()
            .Be("npx -y @modelcontextprotocol/server-filesystem");
    }

    [Fact]
    public async Task GetEdit_WithNpxStdioServer_RendersNpmPackageAndExtraArgumentsInSimpleMode()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        McpServer server;
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
            server = await manager.Create(
                new McpServer
                {
                    Name = $"npx-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Stdio,
                    Command = "npx",
                    Arguments =
                    [
                        "-y",
                        "@modelcontextprotocol/server-filesystem",
                        "/tmp",
                        "--verbose",
                    ],
                }
            );
        }

        // Only an `npx -y <pkg>` Stdio server takes the simple-mode branch in
        // McpServersController.MapServerToDto (UseAdvancedCommand=false), which
        // sets NpmPackage=Arguments[1] and ExtraArguments=Arguments.Skip(2)
        // newline-joined. Every other Edit test seeds an HTTP server and only
        // exercises the advanced else-branch, so this is the sole cover for the
        // npx detection: a regression that mis-slices the args (e.g. Skip(1), or
        // dropping the `-y` guard) would render the package as a raw argument.
        var response = await client.GetAsync($"/McpServers/Edit/{server.Id}", ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);

        document
            .QuerySelector("input[name='NpmPackage']")!
            .GetAttribute("value")
            .Should()
            .Be("@modelcontextprotocol/server-filesystem");

        var extraArguments = document.QuerySelector("textarea[name='ExtraArguments']")!.TextContent;
        extraArguments.Should().Contain("/tmp");
        extraArguments.Should().Contain("--verbose");
    }

    private async Task<McpServer> SeedHttpServerAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        return await manager.Create(
            new McpServer
            {
                Name = name,
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );
    }

    private HttpClient CreateAdminClient() =>
        _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );

    private static async Task<string> HarvestAntiforgeryAsync(
        HttpClient client,
        string path,
        CancellationToken ct
    )
    {
        var response = await client.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        return document.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;
    }
}
