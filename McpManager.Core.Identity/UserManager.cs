using Equibles.Core.AutoWiring;
using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;

namespace McpManager.Core.Identity;

[Service]
public class UserManager
{
    private readonly UserRepository _userRepository;

    public UserManager(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Theme> ToggleTheme(User user)
    {
        user.Theme = user.Theme == Theme.Light ? Theme.Dark : Theme.Light;
        await _userRepository.SaveChanges();
        return user.Theme;
    }

    public async Task<bool> ToggleSidebar(User user)
    {
        user.SidebarCollapsed = !user.SidebarCollapsed;
        await _userRepository.SaveChanges();
        return user.SidebarCollapsed;
    }
}
