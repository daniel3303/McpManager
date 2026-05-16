using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpImportExportManagerSseRoundTripTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpImportExportManagerSseRoundTripTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task ExportThenImport_SseServer_PreservesSseTransportAndUri()
    {
        var ct = TestContext.Current.CancellationToken;
        var name = $"rt-sse-{Guid.NewGuid():N}";
        const string uri = "https://sse-upstream.invalid/mcp";

        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var exportImport = sp.GetRequiredService<McpImportExportManager>();
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var serverRepo = sp.GetRequiredService<McpServerRepository>();

        var original = await serverManager.Create(
            new McpServer
            {
                Name = name,
                TransportType = McpTransportType.Sse,
                Uri = uri,
            }
        );

        // Contract: Export emits SSE servers with transport:"sse" + url, and
        // Import must read that back as an Sse server (not plain Http). PR #340
        // round-tripped Stdio only; the SSE transport-discriminator round-trip
        // (the one place Export writes a "transport" key) was unverified.
        var exported = await exportImport.Export();
        await serverManager.Delete(original);

        var result = await exportImport.Import(exported);

        result.Success.Should().BeTrue();
        result.Imported.Should().BeGreaterThan(0);

        var roundTripped = await serverRepo.GetAll().FirstOrDefaultAsync(s => s.Name == name, ct);

        roundTripped.Should().NotBeNull();
        roundTripped!.TransportType.Should().Be(McpTransportType.Sse);
        roundTripped.Uri.Should().Be(uri);
    }
}
