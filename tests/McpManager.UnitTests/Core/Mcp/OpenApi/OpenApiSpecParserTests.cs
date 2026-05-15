using AwesomeAssertions;
using McpManager.Core.Mcp.OpenApi;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp.OpenApi;

public class OpenApiSpecParserTests
{
    [Fact]
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

    [Fact]
    public void ParseSpec_WithParametersAndJsonBodyNoOperationId_BuildsNameAndInputSchema()
    {
        // The minimal-spec test never exercises BuildOperationName (operationId
        // fallback), BuildParameterList, GetRequestBodyContentType, or the body
        // loop of BuildInputSchema. A spec with path+query params and a JSON
        // body and NO operationId drives all of them: a regression in the
        // path->name sanitisation or the param/body schema merge surfaces here.
        const string yamlSpec = """
            openapi: 3.0.0
            info:
              title: Test API
              version: "1"
            paths:
              /pets/{petId}:
                post:
                  parameters:
                    - name: petId
                      in: path
                      required: true
                      schema:
                        type: string
                    - name: verbose
                      in: query
                      schema:
                        type: boolean
                  requestBody:
                    content:
                      application/json:
                        schema:
                          type: object
                          properties:
                            note:
                              type: string
                  responses:
                    '200':
                      description: OK
            """;

        var operations = new OpenApiSpecParser().ParseSpec(yamlSpec);

        operations.Should().HaveCount(1);
        operations[0].Name.Should().Be("post_pets_petId");
        operations[0]
            .InputSchema.Should()
            .Contain("petId")
            .And.Contain("verbose")
            .And.Contain("note");
    }
}
