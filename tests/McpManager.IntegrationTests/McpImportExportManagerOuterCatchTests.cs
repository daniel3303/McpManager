using AwesomeAssertions;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpImportExportManagerOuterCatchTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpImportExportManagerOuterCatchTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Import_NullJson_ReturnsFailureViaOuterCatch()
    {
        using var scope = _factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<McpImportExportManager>();

        // null makes JToken.Parse throw ArgumentNullException — not a
        // JsonReaderException — so the broad `catch (Exception)` (not the
        // JSON-specific one) records the failure. That outer catch was
        // zero-hit; a regression narrowing it to JsonReaderException would
        // let an unexpected parse error escape and 500 the import endpoint.
        var result = await manager.Import(null);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle().Which.Should().StartWith("Import failed:");
    }
}
