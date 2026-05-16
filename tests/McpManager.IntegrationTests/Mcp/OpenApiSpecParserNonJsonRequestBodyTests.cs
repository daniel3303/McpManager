using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class OpenApiSpecParserNonJsonRequestBodyTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public OpenApiSpecParserNonJsonRequestBodyTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task SyncTools_OperationWithOnlyXmlRequestBody_FallsBackToFirstContentType()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();

        // The request body declares only application/xml — no application/json
        // and no form-encoded — so GetRequestBodyContentType skips the
        // json-preferred branch and hits the `Content.Keys.First()` fallback,
        // which was zero-hit (every other spec uses json or no body). A
        // regression there would mis-type or drop non-JSON-body operations.
        var server = await sut.Create(
            new McpServer
            {
                Name = $"openapi-xml-{Guid.NewGuid():N}",
                TransportType = McpTransportType.OpenApi,
                Uri = "https://api.example.invalid/",
                OpenApiSpecification = """
                openapi: 3.0.0
                info:
                  title: XML API
                  version: "1"
                paths:
                  /upload:
                    post:
                      operationId: uploadThing
                      summary: Upload a thing
                      requestBody:
                        content:
                          application/xml:
                            schema:
                              type: object
                              properties:
                                name:
                                  type: string
                      responses:
                        '200':
                          description: OK
                """,
            }
        );

        var result = await sut.SyncTools(server);

        result.Success.Should().BeTrue();
        result.ToolsAdded.Should().BeGreaterThan(0, "the xml-body operation must still sync");
    }
}
