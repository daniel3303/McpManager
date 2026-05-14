using System.ComponentModel.DataAnnotations;

namespace McpManager.Web.Portal.Dtos.Auth;

public class LoginDto
{
    [Required(ErrorMessage = "This field is required.")]
    [EmailAddress(ErrorMessage = "The email address is not valid.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "This field is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }
}
