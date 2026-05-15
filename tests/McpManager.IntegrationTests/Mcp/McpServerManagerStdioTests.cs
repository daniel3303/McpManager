using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpServerManagerStdioTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServerManagerStdioTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task CheckHealth_AgainstReachableStdioServer_ReturnsTrueAndClearsLastError()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();

        // Spawn the in-tree McpManager.TestStdioServer as a real upstream
        // MCP server over stdio. A successful handshake exercises the
        // McpClientFactory stdio path and the McpServerManager.CheckHealth
        // happy path that the failure-only test cannot cover.
        var server = await sut.Create(NewStdioServer("health"));
        server.LastError = "stale error from a previous run";

        var healthy = await sut.CheckHealth(server);

        healthy.Should().BeTrue();
        server.LastError.Should().BeNull("a successful health check clears the stamped error");
    }

    [Fact]
    public async Task SyncTools_AgainstReachableStdioServer_PersistsTheEchoToolFromTheUpstream()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var tools = scope.ServiceProvider.GetRequiredService<McpToolRepository>();
        var ct = TestContext.Current.CancellationToken;

        var server = await sut.Create(NewStdioServer("sync"));

        var result = await sut.SyncTools(server);

        // The test server publishes exactly one tool (EchoTools.Echo) — the
        // success branch of SyncTools must return Success = true, advertise
        // a non-zero ToolsAdded, and persist the tool to the repository so
        // the playground UI can render it.
        result.Success.Should().BeTrue($"SyncTools should succeed: {result.Error}");
        result.ToolsAdded.Should().BeGreaterThan(0);
        var persisted = await tools.GetAll().Where(t => t.McpServerId == server.Id).ToListAsync(ct);
        persisted.Should().Contain(t => t.Name == "echo");
    }

    private static McpServer NewStdioServer(string suffix) =>
        new()
        {
            Name = $"teststdio-{suffix}-{Guid.NewGuid():N}",
            TransportType = McpTransportType.Stdio,
            Command = "dotnet",
            Arguments = [TestStdioServerLocator.DllPath],
        };
}
