using System.Text.Json;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeysControllerRevealKeyNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeysControllerRevealKeyNotFoundTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetRevealKey_WithUnknownId_ReturnsUnsuccessfulJsonNotASecret()
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

        // RevealKey's not-found branch (line 159) was zero-hit — only the
        // found path is tested. An unknown id must return
        // { success=false, message } and NOT a success-shaped payload; a
        // regression collapsing the guard could surface a null/empty Key as a
        // "successful" reveal in the copy-key UI.
        var response = await client.GetAsync($"/ApiKeys/RevealKey/{Guid.NewGuid()}", ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("message").GetString().Should().Contain("not found");
    }
}
