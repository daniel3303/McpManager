using McpManager.Core.Repositories.Contracts;
using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Identity;

namespace McpManager.Core.Repositories.Identity;

public class UserRepository : BaseRepository<User> {
    public UserRepository(ApplicationDbContext dbContext) : base(dbContext) { }
}
