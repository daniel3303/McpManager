using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class ApiKeyAuthHandlerNamespaceScopeTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeyAuthHandlerNamespaceScopeTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task HandleAuthenticate_KeyScopedToOtherNamespace_RejectsWithUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;

        var allowedSlug = $"allowed{Guid.NewGuid():N}"[..14];
        var deniedSlug = $"denied{Guid.NewGuid():N}"[..14];
        string apiKey;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var nsManager = sp.GetRequiredService<McpNamespaceManager>();
            var allowed = await nsManager.Create(
                new McpNamespace { Name = "Allowed", Slug = allowedSlug }
            );
            await nsManager.Create(new McpNamespace { Name = "Denied", Slug = deniedSlug });

            var keyRepo = sp.GetRequiredService<ApiKeyRepository>();
            var key = await sp.GetRequiredService<ApiKeyManager>()
                .Create(new ApiKey { Name = $"scoped-{Guid.NewGuid():N}" });
            // Scope the key to ONLY the allowed namespace.
            var tracked = await keyRepo.Get(key.Id);
            tracked.AllowedNamespaces.Add(allowed);
            await keyRepo.SaveChanges();
            apiKey = key.Key;
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            apiKey
        );

        // Key is valid but its AllowedNamespaces does not include deniedSlug, so
        // ApiKeyAuthHandler's namespace-scoping branch (hasAccess == false) must
        // Fail authentication -> 401. This authorization boundary was zero-hit;
        // a regression that skipped the AllowedNamespaces check would let a
        // scoped key reach any namespace's tools.
        var response = await client.PostAsync(
            $"/mcp/ns/{deniedSlug}",
            new StringContent("{}", Encoding.UTF8, "application/json"),
            ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
