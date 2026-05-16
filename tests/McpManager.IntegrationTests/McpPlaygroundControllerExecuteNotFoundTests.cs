using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpPlaygroundControllerExecuteNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpPlaygroundControllerExecuteNotFoundTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostExecute_WithUnknownServerId_ReturnsNotFoundJson()
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

        // Execute's `server == null` guard (lines 94-96) was zero-hit — the
        // existing test always uses a real server. An unknown ServerId must
        // return 404 + { success=false, error } so the playground shows an
        // error; a regression dropping the guard would NRE inside
        // McpServerManager.CallTool(null, ...).
        var response = await client.PostAsJsonAsync(
            "/McpPlayground/Execute",
            new
            {
                ServerId = Guid.NewGuid(),
                ToolName = "echo",
                Arguments = new Dictionary<string, object>(),
            },
            ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Server not found");
    }
}
