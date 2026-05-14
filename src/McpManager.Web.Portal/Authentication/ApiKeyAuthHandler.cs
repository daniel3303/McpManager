using System.Security.Claims;
using System.Text.Encodings.Web;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace McpManager.Web.Portal.Authentication;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string ApiKeyNameItemKey = "ApiKeyName";
    public const string ApiKeyIdItemKey = "ApiKeyId";

    private readonly ApiKeyRepository _apiKeyRepository;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiKeyRepository apiKeyRepository
    )
        : base(options, logger, encoder)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = ExtractApiKey();

        if (string.IsNullOrEmpty(key))
        {
            return AuthenticateResult.Fail("Missing or invalid Authorization header");
        }

        var apiKey = await _apiKeyRepository.GetByKey(key).FirstOrDefaultAsync();

        if (apiKey == null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        Context.Items[ApiKeyNameItemKey] = apiKey.Name;
        Context.Items[ApiKeyIdItemKey] = apiKey.Id;

        // Check namespace scoping for namespace proxy endpoints
        var slug = Context.Request.RouteValues["slug"] as string;
        if (!string.IsNullOrEmpty(slug) && apiKey.AllowedNamespaces.Count > 0)
        {
            var hasAccess = apiKey.AllowedNamespaces.Any(n => n.Slug == slug);
            if (!hasAccess)
            {
                return AuthenticateResult.Fail(
                    $"API key does not have access to namespace '{slug}'"
                );
            }
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
            new Claim(ClaimTypes.Name, apiKey.Name),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private string ExtractApiKey()
    {
        if (Request.Headers.ContainsKey("Authorization"))
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader["Bearer ".Length..].Trim();
            }
        }

        return null;
    }
}
