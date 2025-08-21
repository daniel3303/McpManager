using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories.Mcp;

public class McpToolRequestRepository : BaseRepository<McpToolRequest> {
    public McpToolRequestRepository(ApplicationDbContext dbContext) : base(dbContext) { }

    public IQueryable<McpToolRequest> GetByTool(McpTool tool) {
        return GetAll().Where(r => r.McpToolId == tool.Id);
    }
}
