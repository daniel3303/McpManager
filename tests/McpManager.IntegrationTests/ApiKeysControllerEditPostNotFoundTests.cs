using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeysControllerEditPostNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeysControllerEditPostNotFoundTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostEdit_UnknownId_ReturnsNotFound()
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

        ApiKey seeded;
        using (var scope = _factory.Services.CreateScope())
        {
            seeded = await scope
                .ServiceProvider.GetRequiredService<ApiKeyManager>()
                .Create(new ApiKey { Name = $"editnf-{Guid.NewGuid():N}" });
        }

        // Harvest a (not id-bound) antiforgery token from a real key's edit
        // form, then POST it to a random id. The existing Edit-POST tests seed
        // the key, so the `apiKey == null -> NotFound()` guard was zero-hit; a
        // regression dropping it would NRE in Rename for a stale id.
        var getResp = await client.GetAsync($"/ApiKeys/Edit/{seeded.Id}", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token, ["Name"] = "whatever" }
        );

        var response = await client.PostAsync($"/ApiKeys/Edit/{Guid.NewGuid()}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
