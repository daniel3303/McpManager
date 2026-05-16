using AwesomeAssertions;
using McpManager.Core.Mcp.OpenApi;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp.OpenApi;

public class OpenApiSpecParserParseFailureTests
{
    [Fact]
    public void ParseSpec_WithContentThatIsNeitherJsonNorYamlSpec_ThrowsInvalidOperationException()
    {
        var sut = new OpenApiSpecParser();

        // Existing tests only feed valid YAML specs, so the JSON-then-YAML
        // fallback both producing no Paths -> the explicit throw (lines 38-42)
        // was zero-hit. A malformed spec must surface a clear
        // InvalidOperationException (the controllers catch it to show an error);
        // a regression returning an empty operation list would silently accept
        // a broken OpenAPI server definition.
        var act = () => sut.ParseSpec("{\"not\":\"an OpenAPI document\",\"paths\":null}");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Failed to parse OpenAPI specification*");
    }
}
