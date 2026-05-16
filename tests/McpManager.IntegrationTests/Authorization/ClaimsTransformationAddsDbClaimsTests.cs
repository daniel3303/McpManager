using System.Security.Claims;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Repositories.Identity;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Authorization;

public class ClaimsTransformationAddsDbClaimsTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ClaimsTransformationAddsDbClaimsTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task TransformAsync_IdentityMissingDbClaim_HydratesItFromTheUserRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await sp.GetRequiredService<UserRepository>()
            .GetAll()
            .FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);
        var sut = sp.GetRequiredService<IClaimsTransformation>();

        // Build an authenticated identity carrying ONLY the user id — the
        // seeded admin's "Admin" claim lives in the DB, not on this principal.
        // The cookie path already has it, so the per-DB-claim AddClaim branch
        // was zero-hit; pins that TransformAsync hydrates DB claims onto the
        // identity (a regression there breaks every non-admin's permissions).
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString())],
            "TestAuth"
        );

        var result = await sut.TransformAsync(new ClaimsPrincipal(identity));

        result.HasClaim(c => c.Type == "Admin").Should().BeTrue();
    }
}
