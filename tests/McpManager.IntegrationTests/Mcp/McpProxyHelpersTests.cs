using System.Text.Json;
using AwesomeAssertions;
using McpManager.Web.Portal.Mcp;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpProxyHelpersTests
{
    // Pins the comma-separated-type fix in SanitizeSchema: a property typed
    // "integer, string" must become the JSON array ["integer","string"].
    // Claude's 2020-12 API rejects comma strings, so a regression here (e.g.
    // dropping the split or the trim) silently breaks every such tool's schema.
    [Fact]
    public void ParseInputSchema_PropertyWithCommaSeparatedType_SplitsIntoJsonArray()
    {
        const string schema = """
            {"type":"object","properties":{"x":{"type":"integer, string"}}}
            """;

        var result = McpProxyHelpers.ParseInputSchema(schema);

        var typeEl = result.GetProperty("properties").GetProperty("x").GetProperty("type");
        typeEl.ValueKind.Should().Be(JsonValueKind.Array);
        typeEl.EnumerateArray().Select(e => e.GetString()).Should().Equal("integer", "string");
    }
}
