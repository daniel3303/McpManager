using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpImportExportManagerRoundTripTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpImportExportManagerRoundTripTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task ExportThenImport_StdioServer_ReconstructsCommandArgsAndEnv()
    {
        var ct = TestContext.Current.CancellationToken;
        var name = $"rt-{Guid.NewGuid():N}";

        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var exportImport = sp.GetRequiredService<McpImportExportManager>();
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var serverRepo = sp.GetRequiredService<McpServerRepository>();

        var original = await serverManager.Create(
            new McpServer
            {
                Name = name,
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = ["tool.dll", "--port", "0"],
                EnvironmentVariables = new Dictionary<string, string>
                {
                    ["TOKEN"] = "abc123",
                    ["MODE"] = "x",
                },
            }
        );

        // Contract: Export emits servers in the same format Import consumes, so
        // Import(Export()) must faithfully reconstruct the server. Export is
        // tested only for JSON shape and Import only with hand-written JSON —
        // the round-trip (Export's actual output fed back through Import) is
        // unverified. Delete the original first so Import recreates, not skips.
        var exported = await exportImport.Export();
        await serverManager.Delete(original);

        var result = await exportImport.Import(exported);

        result.Success.Should().BeTrue();
        result.Imported.Should().BeGreaterThan(0);

        var roundTripped = await serverRepo.GetAll().FirstOrDefaultAsync(s => s.Name == name, ct);

        roundTripped.Should().NotBeNull();
        roundTripped!.TransportType.Should().Be(McpTransportType.Stdio);
        roundTripped.Command.Should().Be("dotnet");
        roundTripped.Arguments.Should().Equal("tool.dll", "--port", "0");
        roundTripped
            .EnvironmentVariables.Should()
            .ContainKey("TOKEN")
            .WhoseValue.Should()
            .Be("abc123");
        roundTripped.EnvironmentVariables.Should().ContainKey("MODE").WhoseValue.Should().Be("x");
    }
}
