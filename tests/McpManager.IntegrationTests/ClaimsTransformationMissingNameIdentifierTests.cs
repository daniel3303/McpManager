using System.Security.Claims;
using AwesomeAssertions;
using McpManager.Core.Repositories.Identity;
using McpManager.IntegrationTests.Fixtures;
using McpManager.Web.Portal.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class ClaimsTransformationMissingNameIdentifierTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ClaimsTransformationMissingNameIdentifierTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task TransformAsync_AuthenticatedButNoNameIdentifier_ReturnsUnchanged()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = new ClaimsTransformation(
            scope.ServiceProvider.GetRequiredService<UserRepository>()
        );

        // Authenticated identity but no parseable NameIdentifier claim: the
        // guard must return before any _userRepository.Get. That branch was
        // zero-hit (real sign-ins always carry a Guid sub). A regression
        // falling through would Guid.Parse a null/garbage value and 500 every
        // request from a token missing its subject.
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "no-sub-user")], "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var result = await sut.TransformAsync(principal);

        result.Should().BeSameAs(principal);
        result.FindFirst(ClaimTypes.NameIdentifier).Should().BeNull();
    }
}
