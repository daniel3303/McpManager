using AwesomeAssertions;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpImportExportManagerImportInvalidJsonTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpImportExportManagerImportInvalidJsonTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task Import_WithMalformedJson_ReturnsUnsuccessfulResultWithInvalidJsonMessage()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpImportExportManager>();

        // JToken.Parse on malformed input throws JsonReaderException; the
        // dedicated catch was zero-hit (existing tests only pass valid JSON).
        // Import must degrade to an unsuccessful ImportResult with an "Invalid
        // JSON" message, never let the parse exception escape (the /McpServers
        // Import endpoint relies on this to render an error instead of 500).
        var result = await sut.Import("{ this is definitely not valid json ");

        result.Success.Should().BeFalse();
        result.Imported.Should().Be(0);
        result.Messages.Should().Contain(m => m.Contains("Invalid JSON"));
    }
}
