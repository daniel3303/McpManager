using System.ComponentModel.DataAnnotations;
using McpManager.Core.Data.Models.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Core.Data.Models.Identity;

[Index(nameof(Email), IsUnique = true)]
public class User : IdentityUser<Guid>, IActivable
{
    public bool IsActive { get; set; } = true;

    [MaxLength(255)]
    public string GivenName { get; set; }

    [MaxLength(255)]
    public string Surname { get; set; }

    public Theme Theme { get; set; } = Theme.Light;

    public bool SidebarCollapsed { get; set; }

    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
    public string FullName =>
        GivenName + (!string.IsNullOrEmpty(Surname) ? " " + Surname : string.Empty);

    public virtual List<UserLogin> Logins { get; set; } = [];
    public virtual List<UserToken> Tokens { get; set; } = [];
    public virtual List<UserClaim> Claims { get; set; } = [];
    public virtual List<UserRole> Roles { get; set; } = [];

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}
