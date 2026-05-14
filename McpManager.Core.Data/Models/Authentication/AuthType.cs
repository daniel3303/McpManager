using System.ComponentModel.DataAnnotations;

namespace McpManager.Core.Data.Models.Authentication;

public enum AuthType
{
    [Display(Name = "None")]
    None = 0,

    [Display(Name = "Basic")]
    Basic = 1,

    [Display(Name = "Bearer")]
    Bearer = 2,

    [Display(Name = "API Key")]
    ApiKey = 3,

    [Display(Name = "OAuth 2.0")]
    OAuth2 = 4,
}
