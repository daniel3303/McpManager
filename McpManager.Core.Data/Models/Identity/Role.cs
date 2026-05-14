using Microsoft.AspNetCore.Identity;

namespace McpManager.Core.Data.Models.Identity;

public class Role : IdentityRole<Guid>
{
    public virtual List<UserRole> User { get; set; } = [];
}
