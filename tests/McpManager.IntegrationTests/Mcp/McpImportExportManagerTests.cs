using AwesomeAssertions;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
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
}
