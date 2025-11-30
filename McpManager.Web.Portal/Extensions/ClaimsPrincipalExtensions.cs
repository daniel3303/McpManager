using System.Security.Claims;

namespace McpManager.Web.Portal.Extensions;

public static class ClaimsPrincipalExtensions {
    public static Guid GetUserId(this ClaimsPrincipal principal) {
        if (principal == null) {
            throw new ArgumentNullException(nameof(principal));
        }
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim?.Value ?? Guid.Empty.ToString());
    }

    public static bool IsLogged(this ClaimsPrincipal principal) {
        return principal.GetUserId() != Guid.Empty;
    }

    public static bool HasClaim(this ClaimsPrincipal principal, string claimType) {
        return principal?.HasClaim(c => c.Type == claimType) ?? false;
    }
}
