using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpServerManagerCallToolFailureTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServerManagerCallToolFailureTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task CallTool_OnUnreachableStdioUpstream_ReturnsErrorResultWithoutThrowing()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();

        // /usr/bin/false exits non-zero immediately so the stdio MCP client
        // factory throws while negotiating. CallTool's catch must surface
        // the failure as a ToolExecutionResult — never throw to the caller
        // (the controller surfaces result.Error as a flash, the proxy turns
        // it into an MCP error response).
        var server = await sut.Create(
            new McpServer
            {
                Name = $"unreachable-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "/usr/bin/false",
            }
        );

        var result = await sut.CallTool(
            server,
            "any-tool",
            new Dictionary<string, object>(),
            apiKeyName: null,
            namespaceId: null
        );

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CallTool_OnOpenApiServerWithUnknownToolName_ReturnsErrorResultMentioningTool()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();

        // OpenApi branch with an unknown tool name: the lookup throws
        // InvalidOperationException("Tool 'X' not found"). The catch
        // wraps it as a failed ToolExecutionResult and logs to
        // InMemoryLogBuffer + the per-server log table.
        var server = await sut.Create(
            new McpServer
            {
                Name = $"openapi-{Guid.NewGuid():N}",
                TransportType = McpTransportType.OpenApi,
                Uri = "https://api.example.invalid/",
                OpenApiSpecification =
                    "{\"openapi\":\"3.0.0\",\"info\":{\"title\":\"t\",\"version\":\"1\"},\"paths\":{}}",
            }
        );

        var result = await sut.CallTool(
            server,
            "missing-tool",
            new Dictionary<string, object>(),
            apiKeyName: null,
            namespaceId: null
        );

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("missing-tool");
    }
}
