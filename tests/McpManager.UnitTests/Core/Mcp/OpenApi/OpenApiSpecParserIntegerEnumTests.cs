using System.Text.Json;
using AwesomeAssertions;
using McpManager.Core.Mcp.OpenApi;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp.OpenApi;

public class OpenApiSpecParserIntegerEnumTests
{
    [Fact]
    public void ParseSpec_IntegerParamEnum_EmitsNumericEnumValuesNotStrings()
    {
        // Contract: the generated input schema must faithfully represent the
        // OpenAPI operation so an MCP client can build valid tool calls. An
        // integer param with enum [1,2] must keep numeric enum values — string
        // "1"/"2" against type:integer is self-contradictory and Claude's
        // schema validator rejects the integer argument the caller sends.
        const string yamlSpec = """
            openapi: 3.0.0
            info:
              title: T
              version: "1"
            paths:
              /items:
                get:
                  operationId: listItems
                  parameters:
                    - name: status
                      in: query
                      required: true
                      schema:
                        type: integer
                        enum:
                          - 1
                          - 2
                  responses:
                    '200':
                      description: OK
            """;

        var operations = new OpenApiSpecParser().ParseSpec(yamlSpec);

        operations.Should().ContainSingle();
        using var doc = JsonDocument.Parse(operations[0].InputSchema);
        var enumEl = doc
            .RootElement.GetProperty("properties")
            .GetProperty("status")
            .GetProperty("enum");

        enumEl
            .EnumerateArray()
            .Select(e => e.ValueKind)
            .Should()
            .AllBeEquivalentTo(JsonValueKind.Number);
        enumEl.EnumerateArray().Select(e => e.GetInt32()).Should().Equal(1, 2);
    }
}
