using McpManager.Core.Data.Contexts;
using McpManager.Web.Portal.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpManager.Web.Portal.Controllers.Api;

[Authorize]
[Route("api/{controller=Home}/{action=Index}")]
public abstract class ApiController : Controller
{
    protected readonly ApplicationDbContext DbContext;

    protected ApiController(ApplicationDbContext dbContext)
    {
        DbContext = dbContext;
    }

    protected Guid GetAuthenticatedUserId()
    {
        return User.GetUserId();
    }
}
