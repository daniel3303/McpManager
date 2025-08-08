using Microsoft.AspNetCore.Identity;

namespace McpManager.Core.Data.Models.Identity;

public class UserRole : IdentityUserRole<Guid> {
    public virtual User User { get; set; }
    public virtual Role Role { get; set; }
}
