using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories.ApiKeys;

public class ApiKeyRepository : BaseRepository<ApiKey>
{
    public ApiKeyRepository(ApplicationDbContext dbContext)
        : base(dbContext) { }

    public IQueryable<ApiKey> GetByKey(string key)
    {
        return GetAll().Where(k => k.Key == key && k.IsActive);
    }
}
