using System.ComponentModel.DataAnnotations;
using McpManager.Core.Data.Models.Authentication;

namespace McpManager.Web.Portal.Dtos;

public class AuthDto
{
    [Display(Name = "Authentication Type")]
    public AuthType Type { get; set; } = AuthType.None;

    // Basic auth
    [MaxLength(255, ErrorMessage = "Username cannot exceed 255 characters")]
    [Display(Name = "Username")]
    public string Username { get; set; }

    [MaxLength(500, ErrorMessage = "Password cannot exceed 500 characters")]
    [Display(Name = "Password")]
    public string Password { get; set; }

    // Bearer auth
    [MaxLength(2000, ErrorMessage = "Token cannot exceed 2000 characters")]
    [Display(Name = "Bearer Token")]
    public string Token { get; set; }

    // API Key auth
    [MaxLength(255, ErrorMessage = "API Key Name cannot exceed 255 characters")]
    [Display(Name = "API Key Header Name")]
    public string ApiKeyName { get; set; }

    [MaxLength(2000, ErrorMessage = "API Key Value cannot exceed 2000 characters")]
    [Display(Name = "API Key Value")]
    public string ApiKeyValue { get; set; }

    // OAuth2 auth
    [MaxLength(255, ErrorMessage = "Client ID cannot exceed 255 characters")]
    [Display(Name = "Client ID")]
    public string ClientId { get; set; }

    [MaxLength(500, ErrorMessage = "Client Secret cannot exceed 500 characters")]
    [Display(Name = "Client Secret")]
    public string ClientSecret { get; set; }

    [MaxLength(2000, ErrorMessage = "Token Endpoint cannot exceed 2000 characters")]
    [Display(Name = "Token Endpoint")]
    public string TokenEndpoint { get; set; }

    [MaxLength(500, ErrorMessage = "Scope cannot exceed 500 characters")]
    [Display(Name = "Scope")]
    public string Scope { get; set; }
}
