using System.Security.Claims;
using AwesomeAssertions;
using McpManager.Web.Portal.Extensions;
using Xunit;

namespace McpManager.IntegrationTests;

public class ClaimsPrincipalExtensionsIsLoggedTests
{
    [Fact]
    public void IsLogged_PrincipalWithNameIdentifier_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                "TestAuth"
            )
        );

        // IsLogged (and the GetUserId parse it delegates to) was zero-hit —
        // controllers go through GetAuthenticatedUser instead. A regression
        // that compared the parsed id to null instead of Guid.Empty, or threw
        // on a present claim, would break any IsLogged()-gated view logic.
        principal.IsLogged().Should().BeTrue();
    }
}
