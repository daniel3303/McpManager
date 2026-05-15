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
    public async Task PostCreate_WithValidHttpDto_PersistsServerAndRedirectsToShow()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var name = $"created-{Guid.NewGuid():N}";
        var token = await HarvestAntiforgeryAsync(client, "/McpServers/Create", ct);
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = name,
                ["TransportType"] = "Http",
                ["Uri"] = "https://upstream.invalid/mcp",
            }
        );

        // Create POST happy path: ModelState valid -> MapDtoToServer ->
        // McpServerManager.Create -> SyncTools (fails on the invalid host ->
        // Warning branch) -> 302 to Show. Only the ModelState-invalid branch was
        // covered before; asserting the persisted row pins that Create actually
        // ran (a regression rejecting a valid DTO would never persist it).
        var response = await client.PostAsync("/McpServers/Create", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var created = await repo.GetAll().AnyAsync(s => s.Name == name, ct);
        created.Should().BeTrue("Create POST must persist the new server");
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
    public async Task PostImport_WithValidClaudeDesktopJson_CreatesServerAndRendersResult()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var name = $"imported-{Guid.NewGuid():N}";
        var json = "{\"mcpServers\":{\"" + name + "\":{\"url\":\"https://upstream.invalid/mcp\"}}}";
        var token = await HarvestAntiforgeryAsync(client, "/McpServers/Import", ct);
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token, ["json"] = json }
        );

        // Import POST success path: non-empty json -> McpImportExportManager.Import
        // -> result.Success -> success flash + ViewData["ImportResult"] + View
        // (controller lines ~385-406, all uncovered). Asserting the row was
        // actually created pins that the import ran end-to-end, not just that
        // the view rendered.
        var response = await client.PostAsync("/McpServers/Import", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var created = await repo.GetAll().AnyAsync(s => s.Name == name, ct);
        created.Should().BeTrue("Import must persist the server from the Claude Desktop JSON");
    }

    [Fact]
    public async Task PostPreviewOpenApiTools_WithValidSpec_ReturnsParsedToolsJson()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        const string yamlSpec = """
            openapi: 3.0.0
            info:
              title: Test API
              version: "1"
            paths:
              /things:
                get:
                  operationId: listThings
                  summary: List things
                  responses:
                    '200':
                      description: OK
            """;
        var token = await HarvestAntiforgeryAsync(client, "/McpServers/Create", ct);
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["openApiSpecification"] = yamlSpec,
            }
        );

        // PreviewOpenApiTools is the only caller-facing path that runs
        // OpenApiSpecParser.ParseSpec from the controller and projects the
        // operations to JSON (lines ~494-498, all uncovered). A regression in
        // the success-shape (e.g. wrong property casing or losing operationId)
        // surfaces directly in the returned tools array.
        var response = await client.PostAsync("/McpServers/PreviewOpenApiTools", form, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var tools = doc.RootElement.GetProperty("tools");
        tools.GetArrayLength().Should().Be(1);
        tools[0].GetProperty("name").GetString().Should().Be("listThings");
    }

    [Fact]
    public async Task PostEdit_WithExistingIdAndValidChange_PersistsAndRedirectsToShow()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var server = await SeedHttpServerAsync($"edit-post-{Guid.NewGuid():N}");
        var newName = $"renamed-{Guid.NewGuid():N}";
        var token = await HarvestAntiforgeryAsync(client, $"/McpServers/Edit/{server.Id}", ct);
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = newName,
                ["TransportType"] = "Http",
                ["Uri"] = "https://upstream.invalid/mcp",
            }
        );

        // Edit POST happy path: server found + ModelState valid -> MapDtoToServer
        // -> McpServerManager.Update -> SyncTools (fails on the invalid host ->
        // Warning branch) -> 302 to Show. The whole ~50-line action body was
        // uncovered; asserting the persisted rename pins that Update actually
        // ran (a regression short-circuiting before Update keeps the old name).
        var response = await client.PostAsync($"/McpServers/Edit/{server.Id}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var reloaded = await repo.Get(server.Id);
        reloaded!.Name.Should().Be(newName, "Edit POST must persist the new name");
    }

    [Fact]
    public async Task GetEditTool_WithSchemaAndCustomSchema_RendersArgumentWithMergedDescriptions()
    {
        var client = CreateAdminClient();
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var server = await SeedHttpServerAsync($"edittool-{Guid.NewGuid():N}");
        McpTool tool;
        using (var scope = _factory.Services.CreateScope())
        {
            var tools = scope.ServiceProvider.GetRequiredService<McpToolRepository>();
            tool = tools.Add(
                new McpTool
                {
                    Name = "read_file",
                    McpServerId = server.Id,
                    InputSchema = """{"properties":{"path":{"description":"The file path"}}}""",
                    CustomInputSchema =
                        """{"properties":{"path":{"description":"Custom path desc"}}}""",
                }
            );
            await tools.SaveChanges();
        }

        // EditTool GET is the only path that runs the JObject schema parse +
        // custom-schema merge + properties loop (controller lines ~433-453).
        // Asserting on the rendered hidden Name and the CustomDescription input
        // value pins that the original schema yields the arg name and the
        // custom schema overrides its description — a regression in the merge
        // (e.g. reading description off the wrong JObject) flips these.
        var response = await client.GetAsync($"/McpServers/EditTool/{server.Id}/{tool.Id}", ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);

        document
            .QuerySelector("input[name='Arguments[0].Name']")!
            .GetAttribute("value")
            .Should()
            .Be("path");
        document
            .QuerySelector("input[name='Arguments[0].CustomDescription']")!
            .GetAttribute("value")
            .Should()
            .Be("Custom path desc");
        html.Should().Contain("The file path");
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
