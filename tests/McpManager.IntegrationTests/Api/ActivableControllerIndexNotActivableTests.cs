using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests.Api;

public class ActivableControllerIndexNotActivableTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ActivableControllerIndexNotActivableTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostIndex_WithDbSetBackedTypeThatIsNotActivable_ReturnsBadRequest()
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

        // McpTool resolves AND has a DbSet (McpTools) — so it passes the type
        // and DbSet gates — but does NOT implement IActivable. This is the
        // type-confusion guard (line 31): the reflection-by-name endpoint must
        // refuse to toggle arbitrary DbSet entities. Distinct from #192
        // (resolvable IActivable, missing key) and #197 (unresolvable type).
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["modelName"] = "McpManager.Core.Data.Models.Mcp.McpTool, McpManager.Core.Data",
                ["key"] = Guid.NewGuid().ToString(),
            }
        );

        var response = await client.PostAsync("/api/Activable/Index", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("must implement the IActivable interface");
    }
}
