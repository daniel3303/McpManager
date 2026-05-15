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

    [Fact]
    public async Task PostDelete_WithExistingNamespace_RemovesItAndRedirectsToIndex()
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
                    Name = "ToDelete",
                    Slug = "ns-" + Guid.NewGuid().ToString("n")[..8],
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

        // Delete (lines 232-244) was uncovered: found-guard bypass ->
        // NamespaceManager.Delete -> flash + redirect to Index. Asserting the
        // row is gone pins the delete side effect (a regression short-circuiting
        // before Delete would 302 but leave the namespace queryable).
        var response = await client.PostAsync($"/McpNamespaces/Delete/{ns.Id}", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<McpNamespaceRepository>();
        var reloaded = await repo.Get(ns.Id);
        reloaded.Should().BeNull("Delete must remove the namespace row");
    }

    [Fact]
    public async Task GetIndex_WithSearchFilter_ReturnsOnlyMatchingNamespace()
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

        var token = Guid.NewGuid().ToString("n")[..8];
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
            await manager.Create(
                new McpNamespace { Name = $"match-{token}", Slug = $"ns-{token}" }
            );
            await manager.Create(
                new McpNamespace
                {
                    Name = $"other-{Guid.NewGuid():N}",
                    Slug = "ns-" + Guid.NewGuid().ToString("n")[..8],
                }
            );
        }

        // Index (lines 47-67) was entirely uncovered — no test hit the list
        // page. The Name/Slug Contains filter is the branch worth pinning: a
        // regression dropping the Where (or ANDing Name & Slug) would also list
        // the non-matching namespace.
        var response = await client.GetAsync($"/McpNamespaces?Search={token}", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain($"match-{token}");
        body.Should().NotContain("other-", "the search filter must exclude non-matches");
    }

    [Fact]
    public async Task PostRemoveServer_WithLinkedServer_UnlinksItAndRedirectsToShow()
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
        Guid nsServerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var nsManager = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
            ns = await nsManager.Create(
                new McpNamespace
                {
                    Name = "RmSrv",
                    Slug = "ns-" + Guid.NewGuid().ToString("n")[..8],
                }
            );
            var srvManager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
            var server = await srvManager.Create(
                new McpServer
                {
                    Name = $"rmsrv-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
            var link = await nsManager.AddServer(ns, server);
            nsServerId = link.Id;
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

        // RemoveServer (lines 265-274) was uncovered: nsServer found-guard ->
        // NamespaceManager.RemoveServer -> redirect to Show. Asserting the join
        // row is gone pins the unlink (a regression no-op'ing RemoveServer would
        // 302 but keep the server exposed through the namespace endpoint).
        var response = await client.PostAsync(
            $"/McpNamespaces/RemoveServer/{ns.Id}?nsServerId={nsServerId}",
            form,
            ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var nsServerRepo =
            verify.ServiceProvider.GetRequiredService<McpNamespaceServerRepository>();
        var stillLinked = nsServerRepo.GetByNamespace(ns).Any(s => s.Id == nsServerId);
        stillLinked.Should().BeFalse("RemoveServer must drop the namespace-server link");
    }

    [Fact]
    public async Task PostToggleServer_WithLinkedServer_FlipsIsActiveAndReturnsSuccessJson()
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

        Guid nsServerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var nsManager = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
            var ns = await nsManager.Create(
                new McpNamespace
                {
                    Name = "Toggle",
                    Slug = "ns-" + Guid.NewGuid().ToString("n")[..8],
                }
            );
            var srvManager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
            var server = await srvManager.Create(
                new McpServer
                {
                    Name = $"tgl-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
            var link = await nsManager.AddServer(ns, server);
            link.IsActive.Should().BeTrue("a freshly linked server defaults to active");
            nsServerId = link.Id;
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
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["nsServerId"] = nsServerId.ToString(),
                ["isActive"] = "false",
            }
        );

        // ToggleServer (lines 278-286) was uncovered: nsServer found-guard ->
        // NamespaceManager.ToggleServer -> Json success. Asserting the flipped
        // IsActive pins the state mutation (a regression no-op'ing the toggle
        // would still return success while leaving the server exposed).
        var response = await client.PostAsync("/McpNamespaces/ToggleServer", form, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("\"success\":true");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<McpNamespaceServerRepository>();
        var reloaded = await repo.Get(nsServerId);
        reloaded!.IsActive.Should().BeFalse("ToggleServer must persist the disabled state");
    }

    [Fact]
    public async Task PostToggleTool_WithLinkedTool_FlipsIsEnabledAndReturnsSuccessJson()
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

        Guid nsToolId;
        using (var scope = _factory.Services.CreateScope())
        {
            var nsManager = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
            var ns = await nsManager.Create(
                new McpNamespace
                {
                    Name = "TglTool",
                    Slug = "ns-" + Guid.NewGuid().ToString("n")[..8],
                }
            );
            var srvManager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
            var server = await srvManager.Create(
                new McpServer
                {
                    Name = $"tgltool-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
            var link = await nsManager.AddServer(ns, server);
            var tools = scope.ServiceProvider.GetRequiredService<McpToolRepository>();
            var tool = tools.Add(
                new McpTool
                {
                    Name = "do_thing",
                    McpServerId = server.Id,
                    InputSchema = "{}",
                }
            );
            await tools.SaveChanges();
            var nsTools = scope.ServiceProvider.GetRequiredService<McpNamespaceToolRepository>();
            var nsTool = nsTools.Add(
                new McpNamespaceTool
                {
                    McpNamespaceServerId = link.Id,
                    McpToolId = tool.Id,
                    IsEnabled = true,
                }
            );
            await nsTools.SaveChanges();
            nsToolId = nsTool.Id;
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
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["nsToolId"] = nsToolId.ToString(),
                ["isEnabled"] = "false",
            }
        );

        // ToggleTool (lines 288-298) was uncovered: nsTool found-guard ->
        // NamespaceManager.ToggleTool -> Json success. Asserting the flipped
        // IsEnabled pins the mutation (a no-op'd toggle would still report
        // success while the tool stays exposed through the namespace).
        var response = await client.PostAsync("/McpNamespaces/ToggleTool", form, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("\"success\":true");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<McpNamespaceToolRepository>();
        var reloaded = await repo.Get(nsToolId);
        reloaded!.IsEnabled.Should().BeFalse("ToggleTool must persist the disabled state");
    }
}
