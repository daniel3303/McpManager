using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories.Identity;

public class UserRepository : BaseRepository<User>
{
    public UserRepository(ApplicationDbContext dbContext)
        : base(dbContext) { }
}
