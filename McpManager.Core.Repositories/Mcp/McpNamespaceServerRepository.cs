using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories.Mcp;

public class McpNamespaceServerRepository : BaseRepository<McpNamespaceServer> {
    public McpNamespaceServerRepository(ApplicationDbContext dbContext) : base(dbContext) { }

    public IQueryable<McpNamespaceServer> GetByNamespace(McpNamespace ns) =>
        GetAll().Where(s => s.McpNamespaceId == ns.Id);

    public IQueryable<McpNamespaceServer> GetByServer(McpServer server) =>
        GetAll().Where(s => s.McpServerId == server.Id);
}
