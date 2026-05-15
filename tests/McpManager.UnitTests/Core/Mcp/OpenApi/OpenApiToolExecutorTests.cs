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

    [Fact]
    public async Task Execute_PostWithPathQueryAndBodyArgs_BuildsResolvedUrlAndJsonBody()
    {
        HttpRequestMessage seenRequest = null;
        string seenBody = null;
        var sut = new OpenApiToolExecutor(
            new StubHttpClientFactory(req =>
            {
                seenRequest = req;
                seenBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
            })
        );

        var metadata =
            "{\"Method\":\"POST\",\"Path\":\"/items/{id}\",\"Parameters\":["
            + "{\"Name\":\"id\",\"In\":\"path\"},{\"Name\":\"q\",\"In\":\"query\"}]}";
        var tool = new McpTool { Name = "create", Metadata = metadata };
        var server = new McpServer { Uri = "https://api.example.invalid/", Auth = new Auth() };
        var args = new Dictionary<string, object>
        {
            ["id"] = 7,
            ["q"] = "a b",
            ["note"] = "hello",
        };

        // The request-building helpers were uncovered: ResolvePathParameters
        // ({id}->escaped value), BuildQueryString (?q=a%20b), and
        // BuildRequestBody (non-path/query args -> JSON). A regression in path
        // substitution or arg classification sends the upstream a wrong URL or
        // leaks the path/query value into the body.
        var result = await sut.Execute(server, tool, args);

        result.Success.Should().BeTrue($"Execute should succeed: {result.Error}");
        seenRequest!.Method.Should().Be(HttpMethod.Post);
        seenRequest
            .RequestUri!.AbsoluteUri.Should()
            .Be("https://api.example.invalid/items/7?q=a%20b");
        seenBody.Should().Contain("\"note\":\"hello\"");
        seenBody.Should().NotContain("\"id\"", "path args must not leak into the body");
        seenBody.Should().NotContain("\"q\"", "query args must not leak into the body");
    }

    [Fact]
    public async Task Execute_WithBasicAuthAndCustomHeader_SendsAuthorizationAndCustomHeader()
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
            Metadata = "{\"Method\":\"GET\",\"Path\":\"/things\",\"Parameters\":[]}",
        };
        var server = new McpServer
        {
            Uri = "https://api.example.invalid/",
            Auth = new Auth
            {
                Type = AuthType.Basic,
                Username = "alice",
                Password = "s3cret",
            },
            CustomHeaders = { ["X-Tenant"] = "acme" },
        };

        // ConfigureAuth's Basic branch and ConfigureHeaders were uncovered: a
        // regression that swaps user/pass, drops UTF8, or skips custom headers
        // would send the upstream the wrong credentials or omit tenant routing.
        var result = await sut.Execute(server, tool, new Dictionary<string, object>());

        result.Success.Should().BeTrue($"Execute should succeed: {result.Error}");
        seenRequest!.Headers.Authorization!.Scheme.Should().Be("Basic");
        seenRequest
            .Headers.Authorization!.Parameter.Should()
            .Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:s3cret")));
        seenRequest
            .Headers.GetValues("X-Tenant")
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("acme");
    }

    [Fact]
    public async Task Execute_BearerAuthWithUpstream500_SetsErrorAndSendsBearerHeader()
    {
        HttpRequestMessage seenRequest = null;
        var sut = new OpenApiToolExecutor(
            new StubHttpClientFactory(req =>
            {
                seenRequest = req;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    ReasonPhrase = "Internal Server Error",
                };
            })
        );

        var tool = new McpTool
        {
            Name = "list",
            Metadata = "{\"Method\":\"GET\",\"Path\":\"/things\",\"Parameters\":[]}",
        };
        var server = new McpServer
        {
            Uri = "https://api.example.invalid/",
            Auth = new Auth { Type = AuthType.Bearer, Token = "tok-xyz" },
        };

        // Pins ConfigureAuth's Bearer branch and the non-2xx error path together:
        // a regression that Base64-encodes the bearer token, or that reports
        // failed calls as Success=true with no Error, would surface here.
        var result = await sut.Execute(server, tool, new Dictionary<string, object>());

        result.Success.Should().BeFalse();
        result.Error.Should().Be("HTTP 500 Internal Server Error");
        seenRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        seenRequest.Headers.Authorization!.Parameter.Should().Be("tok-xyz");
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
