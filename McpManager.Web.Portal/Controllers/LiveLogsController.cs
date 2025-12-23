using McpManager.Core.Mcp;
using McpManager.Core.Repositories;
using McpManager.Web.Portal.Dtos;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Controllers.Abstract;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Web.Portal.Controllers;

[Authorize(Policy = "McpServers")]
public class LiveLogsController : BaseController {
    private readonly InMemoryLogBuffer _logBuffer;
    private readonly McpServerRepository _serverRepository;

    public LiveLogsController(
        InMemoryLogBuffer logBuffer,
        McpServerRepository serverRepository
    ) {
        _logBuffer = logBuffer;
        _serverRepository = serverRepository;
    }

    public async Task<IActionResult> Index() {
        ViewData["Title"] = "Live Logs";
        ViewData["Menu"] = "LiveLogs";
        ViewData["Icon"] = HeroIcons.Render("signal", size: 5);

        var servers = await _serverRepository.GetAll()
            .OrderBy(s => s.Name)
            .Select(s => new ServerListItem { Id = s.Id, Name = s.Name })
            .ToListAsync();
        ViewData["Servers"] = servers;

        return View();
    }

    [HttpGet]
    public IActionResult Poll(DateTime? since, Guid? serverId, string level) {
        var entries = _logBuffer.GetEntries(since, serverId, level);
        return Json(new { Entries = entries });
    }
}
