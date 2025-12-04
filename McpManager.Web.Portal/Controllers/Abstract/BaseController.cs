using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Web.Portal.Controllers.Abstract;

[Authorize]
[Route("{controller=Home}/{action=Index}")]
public abstract class BaseController : Controller {
    protected async Task<User> GetAuthenticatedUser() {
        var userRepository = HttpContext.RequestServices.GetRequiredService<UserRepository>();
        return await userRepository.Get(User.GetUserId());
    }

    protected Guid GetAuthenticatedUserId() {
        return User.GetUserId();
    }
}
