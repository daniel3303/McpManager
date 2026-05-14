using System.ComponentModel.DataAnnotations;

namespace McpManager.Web.Portal.Dtos.Users;

public class UserForEditDto
{
    [Required(ErrorMessage = "This field is required.")]
    [MaxLength(255)]
    [Display(Name = "Given Name")]
    public string GivenName { get; set; }

    [MaxLength(255)]
    [Display(Name = "Surname")]
    public string Surname { get; set; }

    [Required(ErrorMessage = "This field is required.")]
    [EmailAddress(ErrorMessage = "The email address is not valid.")]
    [Display(Name = "Email")]
    public string Email { get; set; }

    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    [Display(Name = "New Password")]
    public string Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Email Confirmed")]
    public bool EmailConfirmed { get; set; }

    public List<ClaimCheckboxItem> Claims { get; set; } = [];
}
