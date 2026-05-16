using System.Net.Http.Headers;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeyAuthHandlerInactiveKeyRevokedTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeyAuthHandlerInactiveKeyRevokedTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task McpEndpoint_AfterKeyDeactivated_PreviouslyValidKeyIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        string keyString;
        using (var scope = _factory.Services.CreateScope())
        {
            var created = await scope
                .ServiceProvider.GetRequiredService<ApiKeyManager>()
                .Create(new ApiKey { Name = $"rev-{Guid.NewGuid():N}" });
            keyString = created.Key;
        }

        // Security contract: an operator deactivating an API key (ToggleActive)
        // must immediately cut off its /mcp access. The same key is accepted
        // while active and rejected once deactivated — a regression that
        // ignored IsActive would leave "revoked" keys fully working.
        var whileActive = await PostMcpAsync(client, keyString, ct);
        ((int)whileActive.StatusCode)
            .Should()
            .NotBeInRange(401, 403, "an active key must authenticate on /mcp");

        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var key = await sp.GetRequiredService<ApiKeyRepository>()
                .GetByKey(keyString)
                .FirstAsync(ct);
            await sp.GetRequiredService<ApiKeyManager>().ToggleActive(key);
        }

        var afterDeactivation = await PostMcpAsync(client, keyString, ct);
        ((int)afterDeactivation.StatusCode)
            .Should()
            .BeInRange(401, 403, "a deactivated key must be rejected on /mcp");
    }

    private static async Task<HttpResponseMessage> PostMcpAsync(
        HttpClient client,
        string key,
        CancellationToken ct
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}",
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return await client.SendAsync(request, ct);
    }
}
