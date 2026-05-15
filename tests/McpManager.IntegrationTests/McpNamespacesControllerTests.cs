using System.Net;
using System.Text.RegularExpressions;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpNamespacesControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostCreate_WithValidNameAndSlug_RedirectsToShowOfNewNamespace()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;

        await _factory.SignInAsAdminAsync(client, ct);

        var getResponse = await client.GetAsync("/McpNamespaces/Create", ct);
        getResponse.EnsureSuccessStatusCode();
        var html = await getResponse.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var antiForgeryToken = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;

        // A unique slug per run keeps the test idempotent against the shared
        // fixture DB; the slug constraint is ^[a-z0-9][a-z0-9-]*$.
        var slug = "ns-" + Guid.NewGuid().ToString("n")[..8];
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = antiForgeryToken,
                ["Name"] = "Integration test namespace",
                ["Slug"] = slug,
                ["RateLimitRequestsPerMinute"] = "60",
            }
        );

        var response = await client.PostAsync("/McpNamespaces/Create", form, ct);

        // Create() ends in RedirectToAction(Show, new { id = ns.Id }). A
        // regression in McpNamespaces policy, antiforgery, the
        // McpNamespaceManager.Create slug-uniqueness path, or the redirect
        // target would surface here as 200/400/403 or a wrong Location.
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        Regex
            .IsMatch(
                response.Headers.Location!.ToString(),
                @"^/mcpnamespaces/show/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
                RegexOptions.IgnoreCase
            )
            .Should()
            .BeTrue(
                $"Location should match /mcpnamespaces/show/<guid>, was '{response.Headers.Location}'"
            );
    }

    [Fact]
    public async Task PostEdit_WithExistingIdAndValidChange_PersistsAndRedirectsToShow()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        McpNamespace ns;
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
            ns = await manager.Create(
                new McpNamespace
                {
                    Name = "Before",
                    Slug = "ns-" + Guid.NewGuid().ToString("n")[..8],
                }
            );
        }

        var getResp = await client.GetAsync($"/McpNamespaces/Edit/{ns.Id}", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;

        var newName = $"After-{Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = newName,
                ["Slug"] = ns.Slug,
                ["RateLimitRequestsPerMinute"] = "60",
            }
        );

        // Edit POST happy path: namespace found + ModelState valid ->
        // McpNamespaceManager.Update -> 302 to Show. The whole ~40-line action
        // was uncovered; asserting the persisted rename pins that Update ran
        // (a regression short-circuiting on not-found / ApplicationException
        // would keep the old name or return a 200 re-render).
        var response = await client.PostAsync($"/McpNamespaces/Edit/{ns.Id}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verifyScope = _factory.Services.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<McpNamespaceRepository>();
        var reloaded = await repo.Get(ns.Id);
        reloaded!.Name.Should().Be(newName, "Edit POST must persist the new name");
    }

    [Fact]
    public async Task GetShow_WithExistingId_RendersNamespaceWithMcpEndpoint()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var slug = "ns-" + Guid.NewGuid().ToString("n")[..8];
        McpNamespace ns;
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
            ns = await manager.Create(new McpNamespace { Name = "Showcase", Slug = slug });
        }

        // Show was the largest uncovered member (lines 71-111): it runs four
        // repository queries (available servers, ns-servers, ns-tools group-by)
        // and builds the McpEndpoint. Asserting the rendered endpoint pins the
        // whole data-loading path — a regression in any query throws a 500
        // instead of the 200 + endpoint string asserted here.
        var response = await client.GetAsync($"/McpNamespaces/Show/{ns.Id}", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain($"/mcp/ns/{slug}");
        body.Should().Contain("Showcase");
    }

    [Fact]
    public async Task PostAddServer_WithExistingNamespaceAndServer_LinksServerAndRedirectsToShow()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        McpNamespace ns;
        McpServer server;
        using (var scope = _factory.Services.CreateScope())
        {
            var nsManager = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
            ns = await nsManager.Create(
                new McpNamespace
                {
                    Name = "AddSrv",
                    Slug = "ns-" + Guid.NewGuid().ToString("n")[..8],
                }
            );
            var srvManager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
            server = await srvManager.Create(
                new McpServer
                {
                    Name = $"addsrv-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
        }

        var getResp = await client.GetAsync("/McpNamespaces/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        // AddServer (lines 248-261) was uncovered: both not-found guards, the
        // NamespaceManager.AddServer link, and the redirect. Asserting the
        // persisted join row pins the link side effect — a regression that
        // skips AddServer would 302 but leave the namespace empty.
        var response = await client.PostAsync(
            $"/McpNamespaces/AddServer/{ns.Id}?serverId={server.Id}",
            form,
            ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var nsServerRepo =
            verify.ServiceProvider.GetRequiredService<McpNamespaceServerRepository>();
        var linked = nsServerRepo.GetByNamespace(ns).Any(s => s.McpServerId == server.Id);
        linked.Should().BeTrue("AddServer must persist the namespace-server link");
    }
}
