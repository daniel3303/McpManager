using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpServerManagerCallToolOpenApiTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServerManagerCallToolOpenApiTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task CallTool_OpenApiServerWithUnknownTool_ReturnsFailureWithToolNotFound()
    {
        using var scope = _factory.Services.CreateScope();
        var mgr = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var ct = TestContext.Current.CancellationToken;

        var server = await mgr.Create(
            new McpServer
            {
                Name = $"openapi-{Guid.NewGuid():N}",
                TransportType = McpTransportType.OpenApi,
                Uri = "https://api.example.invalid/",
                OpenApiSpecification =
                    "openapi: 3.0.0\ninfo:\n  title: T\n  version: '1'\npaths: {}",
            }
        );

        // No prior test exercised CallTool's OpenApi transport branch: every
        // CallTool test uses Stdio/HTTP. With no synced tools, GetByName returns
        // null and the OpenApi branch throws "Tool '...' not found", which the
        // outer catch turns into a failed ToolExecutionResult. A regression that
        // mis-routed OpenApi servers (or dropped the null guard) would NRE or
        // hit the wrong transport instead of this clean failure.
        var result = await mgr.CallTool(
            server,
            "no-such-operation",
            new Dictionary<string, object>()
        );

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }
}
