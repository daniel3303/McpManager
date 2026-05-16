using System.Text.Json;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpImportExportManagerExportSseTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpImportExportManagerExportSseTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Export_WithSseServer_EmitsTransportSseMarker()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var name = $"sse-{Guid.NewGuid():N}";
        await sp.GetRequiredService<McpServerManager>()
            .Create(
                new McpServer
                {
                    Name = name,
                    TransportType = McpTransportType.Sse,
                    Uri = "https://upstream.invalid/sse",
                }
            );

        // Existing Export tests only cover HTTP/Stdio servers, so the SSE
        // branch (config["transport"] = "sse") was zero-hit. The marker is what
        // lets a re-import distinguish SSE from plain HTTP; a regression
        // dropping it would silently downgrade SSE servers on round-trip.
        var json = await sp.GetRequiredService<McpImportExportManager>().Export();

        using var doc = JsonDocument.Parse(json);
        var server = doc.RootElement.GetProperty("mcpServers").GetProperty(name);
        server.GetProperty("transport").GetString().Should().Be("sse");
        server.GetProperty("url").GetString().Should().Be("https://upstream.invalid/sse");
    }
}
