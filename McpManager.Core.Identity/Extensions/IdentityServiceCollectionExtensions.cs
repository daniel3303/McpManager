using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Identity;
using Equibles.Core.AutoWiring;
using Microsoft.Extensions.DependencyInjection;

namespace McpManager.Core.Identity.Extensions;

public static class IdentityServiceCollectionExtensions {
    public static void AddIdentity(this IServiceCollection services) {
        services.AddIdentityCore<User>(options => {
            options.SignIn.RequireConfirmedEmail = true;
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireDigit = false;
            options.Password.RequiredUniqueChars = 6;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = false;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<ApplicationDbContext>();
        
        services.AutoWireServicesFrom<IdentityAssembly>();
    }
}