using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories.Mcp;

public class McpServerRepository : BaseRepository<McpServer>
{
    public McpServerRepository(ApplicationDbContext dbContext)
        : base(dbContext) { }
}
