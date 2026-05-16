using System.Text.Json;
using AwesomeAssertions;
using McpManager.Web.Portal.Mcp;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpProxyHelpersParseInputSchemaEmptyTests
{
    [Fact]
    public void ParseInputSchema_BlankInput_ReturnsEmptyObjectSchema()
    {
        // The blank/whitespace short-circuit (return {"type":"object"}) was
        // zero-hit — every test passes a real schema. A tool stored with no
        // InputSchema must still yield a valid object schema for Claude's API;
        // a regression here would surface that tool with a null/invalid schema.
        var element = McpProxyHelpers.ParseInputSchema("   ");

        element.ValueKind.Should().Be(JsonValueKind.Object);
        element.GetProperty("type").GetString().Should().Be("object");
    }
}
