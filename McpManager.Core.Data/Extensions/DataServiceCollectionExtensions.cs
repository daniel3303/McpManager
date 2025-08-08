using McpManager.Core.Data.Contexts;
using Equibles.Core.AutoWiring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpManager.Core.Data.Extensions;

public static class DataServiceCollectionExtensions {
    public static void AddData(this IServiceCollection services, IConfiguration configuration, TimeSpan? maxDbCommandTimeout = null) {
        services.AddDbContext<ApplicationDbContext>(options => {
            options.UseSqlite(configuration.GetConnectionString("ApplicationConnection"),
                    b => {
                        var commandTimeout = maxDbCommandTimeout ?? TimeSpan.FromSeconds(30);
                        b.CommandTimeout((int) commandTimeout.TotalSeconds).UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    })
                .UseLazyLoadingProxies().EnableDetailedErrors();
        });
        services.AutoWireServicesFrom<DataAssembly>();
    }
}
