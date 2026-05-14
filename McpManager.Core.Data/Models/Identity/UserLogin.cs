using Microsoft.AspNetCore.Identity;

namespace McpManager.Core.Data.Models.Identity;

public class UserLogin : IdentityUserLogin<Guid>
{
    public virtual User User { get; set; }
}
