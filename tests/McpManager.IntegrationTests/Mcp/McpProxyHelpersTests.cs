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

    // Pins ConvertJsonElement's type mapping: integer JSON numbers → long,
    // arrays → List<object>, objects → Dictionary<string,object>, true → bool.
    // A regression here corrupts every proxied tool-call argument (e.g. ints
    // arriving as double, or objects flattened to strings).
    [Fact]
    public void ConvertArguments_NestedNumberArrayObjectBool_ConvertsToClrShapes()
    {
        using var doc = JsonDocument.Parse("""{"n":42,"flag":true,"arr":[1,2],"obj":{"k":7}}""");
        var args = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        var result = McpProxyHelpers.ConvertArguments(args);

        result["n"].Should().Be(42L);
        result["flag"].Should().Be(true);
        result["arr"].Should().BeOfType<List<object>>().Which.Should().Equal(1L, 2L);
        result["obj"]
            .Should()
            .BeOfType<Dictionary<string, object>>()
            .Which.Should()
            .ContainKey("k")
            .WhoseValue.Should()
            .Be(7L);
    }
}
