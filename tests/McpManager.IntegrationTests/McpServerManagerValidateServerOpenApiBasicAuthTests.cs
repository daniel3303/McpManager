using AwesomeAssertions;
using McpManager.Core.Data.Models.Authentication;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.IntegrationTests;

public class McpServerManagerValidateServerOpenApiBasicAuthTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServerManagerValidateServerOpenApiBasicAuthTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task Create_OpenApiServerBasicAuthBlankUsername_RejectsLikeHttpTransport()
    {
        using var scope = _factory.Services.CreateScope();
        var serverManager = scope.ServiceProvider.GetRequiredService<McpServerManager>();

        var server = new McpServer
        {
            Name = $"vs-oapi-{Guid.NewGuid():N}",
            TransportType = McpTransportType.OpenApi,
            Uri = "https://upstream.invalid/",
            OpenApiSpecification = "{\"openapi\":\"3.0.0\"}",
            Auth = new Auth { Type = AuthType.Basic, Username = "" },
        };

        // Contract: ValidateServer must reject auth that is incomplete for a
        // transport that uses it. The HTTP/SSE branch already enforces
        // "Basic => Username required"; OpenApi consumes Auth identically via
        // OpenApiToolExecutor.ConfigureAuth, so a caller relying on Create to
        // validate usable auth expects the same rejection here.
        var act = async () => await serverManager.Create(server);

        (await act.Should().ThrowAsync<ApplicationException>())
            .Which.Property.Should()
            .Be("Auth.Username");
    }
}
