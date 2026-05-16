using AwesomeAssertions;
using McpManager.Core.Mcp.OpenApi;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp.OpenApi;

public class OpenApiSpecParserJsonBodyRequiredTests
{
    [Fact]
    public void ParseSpec_JsonBodyWithRequiredFields_PropagatesRequiredIntoInputSchema()
    {
        // Existing body tests omit `required` on the body schema, so the
        // `mediaType.Schema.Required` propagation loop was zero-hit. MCP
        // clients use the merged `required` list to validate tool-call args;
        // a regression dropping body-level required would let calls omit
        // mandatory fields and only fail at the upstream API.
        const string yamlSpec = """
            openapi: 3.0.0
            info:
              title: T
              version: "1"
            paths:
              /widgets:
                post:
                  operationId: createWidget
                  requestBody:
                    content:
                      application/json:
                        schema:
                          type: object
                          required:
                            - name
                          properties:
                            name:
                              type: string
                  responses:
                    '200':
                      description: OK
            """;

        var operations = new OpenApiSpecParser().ParseSpec(yamlSpec);

        operations.Should().ContainSingle();
        var schema = operations[0].InputSchema;
        schema.Should().Contain("name");
        schema.Should().Contain("required");
    }
}
