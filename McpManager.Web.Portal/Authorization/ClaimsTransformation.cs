using System.Security.Claims;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using Equibles.Core.AutoWiring;
using Microsoft.AspNetCore.Authentication;

namespace McpManager.Web.Portal.Authorization;

[Service(ServiceLifetime.Scoped, typeof(IClaimsTransformation))]
public class ClaimsTransformation : IClaimsTransformation {
    private readonly UserRepository _userRepository;

    public ClaimsTransformation(UserRepository userRepository) {
        _userRepository = userRepository;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity) {
            return principal;
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) {
            return principal;
        }

        var user = await _userRepository.Get(userId);

        if (user == null) {
            return principal;
        }

        // Add user's claims from database
        foreach (var userClaim in user.Claims) {
            if (!identity.HasClaim(c => c.Type == userClaim.ClaimType && c.Value == userClaim.ClaimValue)) {
                identity.AddClaim(new Claim(userClaim.ClaimType, userClaim.ClaimValue));
            }
        }

        // If user has Admin claim, grant all other claims
        if (identity.HasClaim(c => c.Type == ClaimStore.Get("Admin").Type)) {
            foreach (var claim in ClaimStore.ClaimList()) {
                if (!identity.HasClaim(c => c.Type == claim.Type)) {
                    identity.AddClaim(new Claim(claim.Type, claim.Value));
                }
            }
        }

        return principal;
    }
}
