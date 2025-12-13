using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Identity;
using McpManager.Web.Portal.Controllers.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace McpManager.Web.Portal.Controllers;

public class PersonalSettingsController : BaseController {
    private readonly UserManager _userManager;

    public PersonalSettingsController(UserManager userManager) {
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<IActionResult> ToggleTheme() {
        var user = await GetAuthenticatedUser();
        var theme = await _userManager.ToggleTheme(user);
        return Json(new { theme = theme == Theme.Light ? "mcpmanager" : "mcpmanager-dark" });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleSidebar() {
        var user = await GetAuthenticatedUser();
        var collapsed = await _userManager.ToggleSidebar(user);
        return Json(new { collapsed });
    }
}
