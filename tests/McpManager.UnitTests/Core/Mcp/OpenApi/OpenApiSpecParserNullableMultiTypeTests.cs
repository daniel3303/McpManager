using AwesomeAssertions;
using McpManager.Core.Mcp.OpenApi;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp.OpenApi;

public class OpenApiSpecParserNullableMultiTypeTests
{
    [Fact]
    public void ParseSpec_NullablePropertyWithDescription_EmitsTypeArrayAndDescription()
    {
        // A `nullable: true` scalar makes Microsoft.OpenApi set a flags
        // JsonSchemaType (String|Null), so ConvertSchemaToJObject takes the
        // `types.Count > 1 -> JArray` branch plus the description branch — both
        // zero-hit (existing tests use single-type props). A regression there
        // would drop nullability/description MCP clients use for arg coercion.
        const string yamlSpec = """
            openapi: 3.0.0
            info:
              title: T
              version: "1"
            paths:
              /people:
                post:
                  operationId: createPerson
                  requestBody:
                    content:
                      application/json:
                        schema:
                          type: object
                          properties:
                            nickname:
                              type: string
                              nullable: true
                              description: Optional display name
                  responses:
                    '200':
                      description: OK
            """;

        var operations = new OpenApiSpecParser().ParseSpec(yamlSpec);

        operations.Should().ContainSingle();
        var schema = operations[0].InputSchema;
        schema.Should().Contain("nickname");
        schema.Should().Contain("Optional display name");
        // Multi-type branch serialises the flags enum as a JSON array.
        schema.Should().Contain("null");
    }
}
