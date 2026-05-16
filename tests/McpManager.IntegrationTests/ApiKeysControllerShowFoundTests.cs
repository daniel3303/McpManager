using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeysControllerShowFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeysControllerShowFoundTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetShow_WithExistingKey_RendersKeyDetailPage()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var name = $"show-{Guid.NewGuid():N}";
        ApiKey created;
        using (var scope = _factory.Services.CreateScope())
        {
            created = await scope
                .ServiceProvider.GetRequiredService<ApiKeyManager>()
                .Create(new ApiKey { Name = name });
        }

        // Only Show's not-found redirect was covered; the found path
        // (return View(apiKey)) and Show.cshtml — which renders the key name
        // and the asp-action ToggleActive/Delete forms — were zero-hit. A
        // regression in that path or the view 500s the key-detail page.
        var response = await client.GetAsync($"/ApiKeys/Show/{created.Id}", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain(name);
    }
}
