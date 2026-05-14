using Microsoft.AspNetCore.Identity;

namespace McpManager.Core.Data.Models.Identity;

public class UserToken : IdentityUserToken<Guid>
{
    public virtual User User { get; set; }
}
