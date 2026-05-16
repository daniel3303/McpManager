using AwesomeAssertions;
using McpManager.Web.Portal.Mcp;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpProxyHelpersConvertArgumentsNullTests
{
    [Fact]
    public void ConvertArguments_NullArguments_ReturnsEmptyDictionary()
    {
        // The `arguments == null` short-circuit was zero-hit — every other
        // test passes a populated dictionary. A tools/call with no arguments
        // reaches here; pins that null yields an empty map, not an NRE in the
        // foreach (which would 500 every argument-less proxied tool call).
        var result = McpProxyHelpers.ConvertArguments(null);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
