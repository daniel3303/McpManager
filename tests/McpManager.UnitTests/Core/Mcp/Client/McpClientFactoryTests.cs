using System.Text;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Authentication;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp.Client;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp.Client;

public class McpClientFactoryTests
{
    // Pins the Basic-auth header construction in ConfigureHttpClient: scheme "Basic"
    // and Base64(UTF8("user:pass")) in that exact order. Catches a regression that
    // swaps username/password, drops UTF8, or changes the auth scheme — the MCP
    // handshake still fails (stub returns 500) but the header is set beforehand.
    [Fact]
    public async Task Create_HttpServerWithBasicAuth_SetsBase64BasicAuthorizationHeader()
    {
        var factoryStub = new CapturingHttpClientFactory();
        var sut = new McpClientFactory(factoryStub);
        var server = new McpServer
        {
            Name = "basic-auth-server",
            TransportType = McpTransportType.Http,
            Uri = "http://localhost:1/mcp",
            Auth = new Auth
            {
                Type = AuthType.Basic,
                Username = "alice",
                Password = "s3cret",
            },
        };

        var act = async () => await sut.Create(server);

        await act.Should().ThrowAsync<Exception>();
        var auth = factoryStub.Created.DefaultRequestHeaders.Authorization;
        auth.Should().NotBeNull();
        auth!.Scheme.Should().Be("Basic");
        auth.Parameter.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:s3cret")));
    }

    // Pins the Bearer-auth branch in ConfigureHttpClient: scheme "Bearer" and the
    // raw token as the parameter (no Base64, no transformation). Catches a regression
    // that Base64-encodes the token like Basic, or emits the wrong scheme.
    [Fact]
    public async Task Create_HttpServerWithBearerAuth_SetsBearerTokenAuthorizationHeader()
    {
        var factoryStub = new CapturingHttpClientFactory();
        var sut = new McpClientFactory(factoryStub);
        var server = new McpServer
        {
            Name = "bearer-auth-server",
            TransportType = McpTransportType.Http,
            Uri = "http://localhost:1/mcp",
            Auth = new Auth { Type = AuthType.Bearer, Token = "tok-abc-123" },
        };

        var act = async () => await sut.Create(server);

        await act.Should().ThrowAsync<Exception>();
        var auth = factoryStub.Created.DefaultRequestHeaders.Authorization;
        auth.Should().NotBeNull();
        auth!.Scheme.Should().Be("Bearer");
        auth.Parameter.Should().Be("tok-abc-123");
    }

    private sealed class CapturingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient Created { get; private set; }

        public HttpClient CreateClient(string name) =>
            Created = new HttpClient(new FailingHandler());

        private sealed class FailingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            ) =>
                Task.FromResult(
                    new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                );
        }
    }
}
