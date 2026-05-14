using System.Text.RegularExpressions;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class ApiKeyManagerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeyManagerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Create_PopulatesKeyWithMcpmPrefixAndUrlSafeBody()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<ApiKeyManager>();

        var apiKey = await sut.Create(new ApiKey { Name = $"integration-test-{Guid.NewGuid():N}" });

        // GenerateApiKey takes 32 random bytes and base64-encodes them with
        // +, /, and = stripped, then prefixes "mcpm_". The wire format is
        // documented to callers as "mcpm_<url-safe-token>" — any regression
        // in the prefix or the URL-safety stripping would silently produce
        // tokens that fail Bearer parsing or routing.
        apiKey.Key.Should().StartWith("mcpm_");
        Regex
            .IsMatch(apiKey.Key, @"^mcpm_[A-Za-z0-9]+$")
            .Should()
            .BeTrue($"key body must be URL-safe alphanumeric, was '{apiKey.Key}'");
        // 32 random bytes -> 44-char unpadded base64 worst case; allow margin.
        apiKey.Key.Length.Should().BeGreaterThan("mcpm_".Length + 32);
    }
}
