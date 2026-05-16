using System.Security.Claims;
using AwesomeAssertions;
using McpManager.Web.Portal.Extensions;
using Xunit;

namespace McpManager.IntegrationTests;

public class ClaimsPrincipalExtensionsHasClaimTests
{
    [Fact]
    public void HasClaim_PrincipalCarryingClaimType_ReturnsTrue()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("perm.McpServers", "true")], "TestAuth")
        );

        // The type-only HasClaim overload was zero-hit (call sites use the
        // built-in predicate form). Permission-gated UI calls this to decide
        // whether to render an action; a regression in the type match would
        // hide or wrongly show admin controls.
        principal.HasClaim("perm.McpServers").Should().BeTrue();
        principal.HasClaim("perm.Missing").Should().BeFalse();
    }
}
