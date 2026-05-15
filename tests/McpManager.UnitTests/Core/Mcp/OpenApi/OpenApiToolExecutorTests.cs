using System.Net;
using System.Text;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Authentication;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp.OpenApi;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp.OpenApi;

public class OpenApiToolExecutorTests
{
    [Fact]
    public async Task CheckHealth_WhenUpstreamReturns200_ReturnsTrue()
    {
        var sut = new OpenApiToolExecutor(
            new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK))
        );

        // CheckHealth treats any non-5xx as healthy — a 200 from the stub
        // upstream must produce true. This is the OpenAPI-transport mirror
        // of McpServerManager's CheckHealth happy path; it's the only place
        // that exercises HEAD-based probing of an OpenAPI server.
        var healthy = await sut.CheckHealth(
            new McpServer { Uri = "https://upstream.invalid/", Auth = new Auth() }
        );

        healthy.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_OnGetWithJsonBody_ReturnsBodyTextOnSuccessResult()
    {
        HttpRequestMessage seenRequest = null;
        var sut = new OpenApiToolExecutor(
            new StubHttpClientFactory(req =>
            {
                seenRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"value\":42}",
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            })
        );

        // Minimal metadata exercises the GET path on a no-parameter operation.
        // A regression in URL construction (path concatenation, trailing-slash
        // trim) or response-body extraction would surface here as a failed
        // Execute or a missing body.
        var metadata = "{\"Method\":\"GET\",\"Path\":\"/things\",\"Parameters\":[]}";
        var tool = new McpTool { Name = "list", Metadata = metadata };
        var server = new McpServer { Uri = "https://api.example.invalid/", Auth = new Auth() };

        var result = await sut.Execute(server, tool, new Dictionary<string, object>());

        result.Success.Should().BeTrue($"Execute should succeed: {result.Error}");
        result.Content.Should().Contain(c => c.Type == "text" && c.Text == "{\"value\":42}");
        seenRequest.Should().NotBeNull();
        seenRequest!.Method.Should().Be(HttpMethod.Get);
        seenRequest.RequestUri!.AbsoluteUri.Should().Be("https://api.example.invalid/things");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handler = handler;

        public HttpClient CreateClient(string name) => new(new StubHandler(_handler));

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
                _handler = handler;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            ) => Task.FromResult(_handler(request));
        }
    }
}
