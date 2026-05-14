using System.ComponentModel.DataAnnotations;

namespace McpManager.Core.Data.Models.Authentication;

public class Auth
{
    public AuthType Type { get; set; } = AuthType.None;

    // Basic auth fields
    [MaxLength(255)]
    public string Username { get; set; }

    [MaxLength(500)]
    public string Password { get; set; }

    // Bearer token
    [MaxLength(2000)]
    public string Token { get; set; }

    // API Key fields
    [MaxLength(255)]
    public string ApiKeyName { get; set; }

    [MaxLength(2000)]
    public string ApiKeyValue { get; set; }

    // OAuth2 fields
    [MaxLength(255)]
    public string ClientId { get; set; }

    [MaxLength(500)]
    public string ClientSecret { get; set; }

    [MaxLength(2000)]
    public string TokenEndpoint { get; set; }

    [MaxLength(500)]
    public string Scope { get; set; }
}
