using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories.Mcp;

public class McpNamespaceRepository : BaseRepository<McpNamespace>
{
    public McpNamespaceRepository(ApplicationDbContext dbContext)
        : base(dbContext) { }

    public IQueryable<McpNamespace> GetBySlug(string slug) => GetAll().Where(n => n.Slug == slug);
}
