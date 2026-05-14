using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories.Mcp;

public class McpServerLogRepository : BaseRepository<McpServerLog>
{
    public McpServerLogRepository(ApplicationDbContext dbContext)
        : base(dbContext) { }

    public IQueryable<McpServerLog> GetByServer(McpServer server)
    {
        return GetAll()
            .Where(l => l.McpServerId == server.Id)
            .OrderByDescending(l => l.CreationTime);
    }
}
