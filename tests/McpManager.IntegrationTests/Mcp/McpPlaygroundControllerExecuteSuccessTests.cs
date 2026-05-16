using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpPlaygroundControllerExecuteSuccessTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpPlaygroundControllerExecuteSuccessTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostExecute_RealSyncedStdioTool_ReturnsSuccessWithUpstreamContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var serverId = await SeedSyncedStdioServerAsync();

        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        await _factory.SignInAsAdminAsync(client, ct);

        // Contract: the Playground Execute endpoint runs the chosen tool on the
        // chosen server and returns the upstream result. Only the
        // server-not-found guard was tested — the happy path (the feature's
        // entire purpose: execute a real tool, see its output) was unverified.
        var response = await client.PostAsJsonAsync(
            "/McpPlayground/Execute",
            new
            {
                ServerId = serverId,
                ToolName = "echo",
                Arguments = new Dictionary<string, object> { ["message"] = "playground-hi" },
            },
            ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var texts = doc
            .RootElement.GetProperty("content")
            .EnumerateArray()
            .Select(c => c.GetProperty("text").GetString());
        texts.Should().Contain(t => t != null && t.Contains("playground-hi"));
    }

    private async Task<Guid> SeedSyncedStdioServerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var serverManager = scope.ServiceProvider.GetRequiredService<McpServerManager>();

        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"pg-exec-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );
        var sync = await serverManager.SyncTools(server);
        sync.Success.Should().BeTrue($"SyncTools precondition (echo must exist): {sync.Error}");
        return server.Id;
    }
}
