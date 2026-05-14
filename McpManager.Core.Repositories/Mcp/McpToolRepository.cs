using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories.Mcp;

public class McpToolRepository : BaseRepository<McpTool>
{
    public McpToolRepository(ApplicationDbContext dbContext)
        : base(dbContext) { }

    public IQueryable<McpTool> GetByServer(McpServer server)
    {
        return GetAll().Where(t => t.McpServerId == server.Id);
    }

    public IQueryable<McpTool> GetByName(McpServer server, string toolName)
    {
        return GetAll().Where(t => t.McpServerId == server.Id && t.Name == toolName);
    }
}
