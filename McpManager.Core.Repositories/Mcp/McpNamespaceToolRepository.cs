using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories.Mcp;

public class McpNamespaceToolRepository : BaseRepository<McpNamespaceTool>
{
    public McpNamespaceToolRepository(ApplicationDbContext dbContext)
        : base(dbContext) { }

    public IQueryable<McpNamespaceTool> GetByNamespaceServer(McpNamespaceServer nsServer) =>
        GetAll().Where(t => t.McpNamespaceServerId == nsServer.Id);
}
