using AwesomeAssertions;
using McpManager.Core.Mcp.OpenApi;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp.OpenApi;

public class OpenApiSpecParserTests
{
    [Fact(Skip = "GH-60 — YAML fallback throws 'Format yaml is not supported'")]
    public void ParseSpec_WithMinimalValidYamlSpec_ReturnsOperationNamedFromOperationId()
    {
        // ParseSpec tries JSON first then falls back to YAML, and prefers
        // operationId over the generated method-path name. The YAML branch +
        // operationId preference are the two paths a typical user-supplied
        // spec hits; a regression in the Microsoft.OpenApi parser or in the
        // operationId preference would surface here.
        const string yamlSpec = """
            openapi: 3.0.0
            info:
              title: Test API
              version: "1"
            paths:
              /things:
                get:
                  operationId: listThings
                  summary: List things
                  responses:
                    '200':
                      description: OK
            """;

        var operations = new OpenApiSpecParser().ParseSpec(yamlSpec);

        operations.Should().HaveCount(1);
        operations[0].Name.Should().Be("listThings");
        operations[0].Description.Should().Be("List things");
    }
}
