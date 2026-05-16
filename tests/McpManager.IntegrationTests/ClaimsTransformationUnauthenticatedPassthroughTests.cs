using System.Security.Claims;
using AwesomeAssertions;
using McpManager.Core.Repositories.Identity;
using McpManager.IntegrationTests.Fixtures;
using McpManager.Web.Portal.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class ClaimsTransformationUnauthenticatedPassthroughTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ClaimsTransformationUnauthenticatedPassthroughTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task TransformAsync_UnauthenticatedPrincipal_ReturnsItUnchanged()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = new ClaimsTransformation(
            scope.ServiceProvider.GetRequiredService<UserRepository>()
        );

        // An anonymous identity is not ClaimsIdentity{IsAuthenticated:true},
        // so the guard returns the principal before any DB lookup. That
        // early-out was zero-hit (every test request is authenticated). A
        // regression that fell through would hit the repo on every
        // unauthenticated request (e.g. the login page) — needless load and a
        // potential null deref.
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await sut.TransformAsync(principal);

        result.Should().BeSameAs(principal);
        result.Identity!.IsAuthenticated.Should().BeFalse();
        result.Claims.Should().BeEmpty();
    }
}
