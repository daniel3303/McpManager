using System.Net;
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

public class McpNamespacesControllerEditDuplicateSlugTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerEditDuplicateSlugTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostEdit_ChangingSlugToOneOwnedByAnother_ReRendersWithoutPersisting()
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

        var slugA = "nsa-" + Guid.NewGuid().ToString("n")[..8];
        var slugB = "nsb-" + Guid.NewGuid().ToString("n")[..8];
        Guid idB;
        using (var scope = _factory.Services.CreateScope())
        {
            var mgr = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
            await mgr.Create(new McpNamespace { Name = "First", Slug = slugA });
            var b = new McpNamespace { Name = "Second", Slug = slugB };
            await mgr.Create(b);
            idB = b.Id;
        }

        var getResp = await client.GetAsync($"/McpNamespaces/Edit/{idB}", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("form input[name='AntiForgery']")!.GetAttribute("value")!;

        // ModelState is valid, so Edit() reaches NamespaceManager.Update which
        // throws ApplicationException from ValidateSlugUnique (slug owned by the
        // other namespace). Only the catch (ApplicationException) branch turns
        // that into a ModelState error + Form re-render; it was zero-hit (the
        // happy-path Edit tests keep the slug unique). A regression letting the
        // exception escape would 500 instead of re-rendering 200.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "Second-renamed",
                ["Slug"] = slugA,
                ["RateLimitRequestsPerMinute"] = "60",
            }
        );

        var response = await client.PostAsync($"/McpNamespaces/Edit/{idB}", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "a colliding slug must re-render the form, not redirect");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<McpNamespaceRepository>();
        var reloaded = await repo.GetAll().SingleAsync(n => n.Id == idB, ct);
        reloaded.Slug.Should().Be(slugB, "the rejected edit must not have been persisted");
    }
}
