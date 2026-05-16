using System.Net;
using System.Text;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Authentication;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp.OpenApi;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp.OpenApi;

public class OpenApiToolExecutorBooleanQueryTests
{
    [Fact(Skip = "GH-330 — OpenApiToolExecutor serializes boolean query args as .NET \"True\"")]
    public async Task Execute_BooleanQueryArgument_SerializesAsLowercaseTrue()
    {
        HttpRequestMessage seenRequest = null;
        var sut = new OpenApiToolExecutor(
            new StubHttpClientFactory(req =>
            {
                seenRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
            })
        );

        var tool = new McpTool
        {
            Name = "list",
            Metadata =
                "{\"Method\":\"GET\",\"Path\":\"/things\",\"Parameters\":["
                + "{\"Name\":\"active\",\"In\":\"query\"}]}",
        };
        var server = new McpServer { Uri = "https://api.example.invalid/", Auth = new Auth() };
        var args = new Dictionary<string, object> { ["active"] = true };

        // Contract: Execute must build the upstream request faithfully from the
        // operation + MCP args. OpenAPI/JSON booleans are lowercase, so a `true`
        // query arg must serialize as active=true. .NET bool.ToString() yields
        // "True" — real APIs reject/misparse it (same class as GH-328).
        var result = await sut.Execute(server, tool, args);

        result.Success.Should().BeTrue($"Execute should succeed: {result.Error}");
        seenRequest!
            .RequestUri!.AbsoluteUri.Should()
            .Be("https://api.example.invalid/things?active=true");
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
