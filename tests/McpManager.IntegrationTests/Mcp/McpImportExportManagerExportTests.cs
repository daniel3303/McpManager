using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpImportExportManagerExportTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpImportExportManagerExportTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Export_WithBothStdioAndHttpServers_EmitsClaudeDesktopFormat()
    {
        using var scope = _factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var sut = scope.ServiceProvider.GetRequiredService<McpImportExportManager>();

        // Seed one stdio (command/args/env branch) and one http (url branch).
        // Use unique names so the assertion finds them even when the shared
        // fixture DB has leftover rows from earlier tests.
        var stdioName = $"export-stdio-{Guid.NewGuid():N}";
        var httpName = $"export-http-{Guid.NewGuid():N}";
        await manager.Create(
            new McpServer
            {
                Name = stdioName,
                TransportType = McpTransportType.Stdio,
                Command = "/usr/bin/echo",
                Arguments = ["hello"],
                EnvironmentVariables = new Dictionary<string, string> { ["FOO"] = "bar" },
            }
        );
        await manager.Create(
            new McpServer
            {
                Name = httpName,
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );

        var json = await sut.Export();

        // The Claude Desktop config format is a top-level object with a
        // "mcpServers" map. The stdio branch must emit command/args/env;
        // the http branch must emit url. A regression in the transport
        // switch (e.g. stdio falling through to else) would surface here.
        var root = JObject.Parse(json);
        var servers = root["mcpServers"]!.Should().BeOfType<JObject>().Subject;
        var stdio = servers[stdioName]!.Should().BeOfType<JObject>().Subject;
        stdio["command"]!.Value<string>().Should().Be("/usr/bin/echo");
        ((JArray)stdio["args"]!)[0].Value<string>().Should().Be("hello");
        stdio["env"]!["FOO"]!.Value<string>().Should().Be("bar");
        var http = servers[httpName]!.Should().BeOfType<JObject>().Subject;
        http["url"]!.Value<string>().Should().Be("https://upstream.invalid/mcp");
    }
}
