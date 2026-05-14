using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class UsersControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UsersControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetIndex_AsAuthenticatedAdmin_RendersListContainingSeededAdmin()
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

        var response = await client.GetAsync("/Users", ct);
        response.EnsureSuccessStatusCode();

        // UsersController inherits BaseController's [Authorize] only — no
        // policy guard, unlike McpServers/ApiKeys/AdminSettings. The seeded
        // admin row must show up in the rendered list; a missing seed or a
        // broken UserRepository.GetAll() would render an empty grid that
        // still returns 200.
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("admin@mcpmanager.local");
    }
}
