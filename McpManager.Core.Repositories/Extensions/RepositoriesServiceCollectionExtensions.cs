using McpManager.Core.Repositories.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace McpManager.Core.Repositories.Extensions;

public static class RepositoriesServiceCollectionExtensions {
    public static bool IsSubClassOfGenericType(this Type type, Type genericType) {
        var currentType = type;
        while (currentType != null) {
            if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == genericType) {
                return true;
            }

            currentType = currentType.BaseType;
        }

        return false;
    }

    public static void AddRepositories(this IServiceCollection services) {
        var repositories =
            typeof(BaseRepository<>).Assembly.DefinedTypes.Where(t =>
                t is { IsClass: true, IsAbstract: false, IsInterface: false } &&
                t.IsSubClassOfGenericType(typeof(BaseRepository<>)));
        foreach (var repository in repositories) {
            services.Add(new ServiceDescriptor(repository, repository, ServiceLifetime.Scoped));
        }
    }
}
