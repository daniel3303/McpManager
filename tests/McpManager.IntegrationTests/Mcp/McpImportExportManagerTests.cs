using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpImportExportManagerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpImportExportManagerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Import_WithMalformedJson_ReturnsFailureResultWithoutThrowing()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpImportExportManager>();

        // Import is a bulk operation called from the McpServers UI; the
        // contract is that bad JSON returns a friendly result object with
        // Success = false and a diagnostic message — never throws. A
        // regression that lets JsonReaderException escape would 500 the
        // import page instead of showing the user what went wrong.
        var result = await sut.Import("this is not json {");

        result.Success.Should().BeFalse();
        result.Imported.Should().Be(0);
        result.Messages.Should().ContainSingle(m => m.StartsWith("Invalid JSON"));
    }

    [Fact]
    public async Task Import_WithStdioConfig_BuildsStdioServerWithCommandArgsAndEnv()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpImportExportManager>();
        var ct = TestContext.Current.CancellationToken;

        var name = $"stdio-import-{Guid.NewGuid():N}";
        var json =
            "{\"mcpServers\":{\""
            + name
            + "\":{\"command\":\"node\",\"args\":[\"server.js\",\"--port\",\"0\"],"
            + "\"env\":{\"API_KEY\":\"secret\"}}}}";

        // BuildServerFromConfig's Stdio branch (config["command"] present ->
        // Command/args/env) was uncovered — the only import test used an HTTP
        // url config. A regression mis-mapping args/env would import a broken
        // Stdio server that fails to launch.
        var result = await sut.Import(json);

        result.Success.Should().BeTrue();
        result.Imported.Should().BeGreaterThan(0);

        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var created = await repo.GetAll().FirstAsync(s => s.Name == name, ct);
        created.TransportType.Should().Be(McpTransportType.Stdio);
        created.Command.Should().Be("node");
        created.Arguments.Should().Equal("server.js", "--port", "0");
        created
            .EnvironmentVariables.Should()
            .ContainKey("API_KEY")
            .WhoseValue.Should()
            .Be("secret");
    }
}
