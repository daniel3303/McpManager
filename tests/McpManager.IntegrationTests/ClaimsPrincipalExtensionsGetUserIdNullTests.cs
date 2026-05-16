using System.Security.Claims;
using AwesomeAssertions;
using McpManager.Web.Portal.Extensions;
using Xunit;

namespace McpManager.IntegrationTests;

public class ClaimsPrincipalExtensionsGetUserIdNullTests
{
    [Fact]
    public void GetUserId_NullPrincipal_ThrowsArgumentNullException()
    {
        // The `principal == null` guard was zero-hit (every caller passes the
        // request's User). Pins that a null principal fails fast with a clear
        // ArgumentNullException rather than an NRE in FindFirst — a regression
        // dropping the guard would obscure misuse at call sites.
        var act = () => ((ClaimsPrincipal)null).GetUserId();

        act.Should().Throw<ArgumentNullException>().WithParameterName("principal");
    }
}
