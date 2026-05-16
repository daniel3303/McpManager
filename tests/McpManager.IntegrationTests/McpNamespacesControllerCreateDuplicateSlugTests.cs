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

public class McpNamespacesControllerCreateDuplicateSlugTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerCreateDuplicateSlugTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostCreate_WithDuplicateSlug_ReRendersFormWithoutCreatingSecond()
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

        var slug = "nsdup-" + Guid.NewGuid().ToString("n")[..8];
        using (var scope = _factory.Services.CreateScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<McpNamespaceManager>()
                .Create(new McpNamespace { Name = "First", Slug = slug });
        }

        var getResp = await client.GetAsync("/McpNamespaces/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("form input[name='AntiForgery']")!.GetAttribute("value")!;

        // ModelState is valid, so Create() calls NamespaceManager.Create which
        // throws ApplicationException from ValidateSlugUnique. Only the catch
        // (lines 152-155) turns that into a ModelState error + Form re-render;
        // it was zero-hit (existing Create tests use unique slugs). A
        // regression letting the exception escape would 500 instead of 200.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "Second",
                ["Slug"] = slug,
                ["RateLimitRequestsPerMinute"] = "60",
            }
        );

        var response = await client.PostAsync("/McpNamespaces/Create", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "duplicate slug must re-render, not redirect");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<McpNamespaceRepository>();
        (await repo.GetAll().CountAsync(n => n.Slug == slug, ct))
            .Should()
            .Be(1, "the duplicate must not have been persisted");
    }
}
