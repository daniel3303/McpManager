using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Controllers.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Web.Portal.Controllers;

public class HomeController : BaseController
{
    private readonly McpServerRepository _mcpServerRepository;
    private readonly McpToolRepository _toolRepository;
    private readonly ApiKeyRepository _apiKeyRepository;

    public HomeController(
        McpServerRepository mcpServerRepository,
        McpToolRepository toolRepository,
        ApiKeyRepository apiKeyRepository
    )
    {
        _mcpServerRepository = mcpServerRepository;
        _toolRepository = toolRepository;
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Home";
        ViewData["Menu"] = "Home";
        ViewData["TotalServers"] = await _mcpServerRepository.GetAll().CountAsync();
        ViewData["ActiveServers"] = await _mcpServerRepository.GetAll().CountAsync(s => s.IsActive);
        ViewData["TotalTools"] = await _toolRepository.GetAll().CountAsync();
        ViewData["TotalApiKeys"] = await _apiKeyRepository.GetAll().CountAsync(k => k.IsActive);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ActiveApiKey()
    {
        var apiKey = await _apiKeyRepository
            .GetAll()
            .Where(k => k.IsActive)
            .OrderBy(k => k.CreationTime)
            .Select(k => new { k.Key })
            .FirstOrDefaultAsync();

        if (apiKey == null)
        {
            return Json(new { Success = false, Message = "No active API key found." });
        }

        return Json(new { Success = true, apiKey.Key });
    }

    [Route("/Home/Error")]
    [AllowAnonymous]
    public IActionResult Error()
    {
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;

        ViewData["Title"] = "Error";
        ViewData["Exception"] = exception;
        ViewData["RequestPath"] = exceptionFeature?.Path;

        return View("~/Views/Error/Index.cshtml");
    }
}
