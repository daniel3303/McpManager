using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpNamespacesControllerAddServerNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerAddServerNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostAddServer_ExistingNamespaceUnknownServer_ReturnsNotFound()
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
            ns = await scope
                .ServiceProvider.GetRequiredService<McpNamespaceManager>()
                .Create(
                    new McpNamespace
                    {
                        Name = "AddSrvNF",
                        Slug = "nsnf-" + Guid.NewGuid().ToString("n")[..8],
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

        // Namespace resolves (skips the first guard) but the serverId does not,
        // so AddServer's second guard (server == null -> NotFound) fires — a
        // line the happy-path test never reaches. A regression dropping it
        // would NRE inside NamespaceManager.AddServer(ns, null) -> 500 not 404.
        var response = await client.PostAsync(
            $"/McpNamespaces/AddServer/{ns.Id}?serverId={Guid.NewGuid()}",
            form,
            ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
