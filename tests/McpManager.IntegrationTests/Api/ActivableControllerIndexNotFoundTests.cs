using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests.Api;

public class ActivableControllerIndexNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ActivableControllerIndexNotFoundTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostIndex_WithResolvableTypeButMissingKey_ReturnsNotFound()
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

        // McpServer resolves, has a DbSet, and implements IActivable, so control
        // flows past every BadRequest guard into the dynamic `dbSet.Find(key)`.
        // A random key makes Find return null -> the `model == null` NotFound
        // branch (lines 33-36), which no existing test reached: the other tests
        // either find the row or trip the User guard first. A regression that
        // dropped the null check would NRE on `model.IsActive` -> 500 not 404.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["modelName"] = "McpManager.Core.Data.Models.Mcp.McpServer, McpManager.Core.Data",
                ["key"] = Guid.NewGuid().ToString(),
            }
        );

        var response = await client.PostAsync("/api/Activable/Index", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("was not found");
    }
}
