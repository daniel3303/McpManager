using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories;

public class AppSettingsRepository : BaseRepository<AppSettings> {
    public AppSettingsRepository(ApplicationDbContext dbContext) : base(dbContext) { }
}
