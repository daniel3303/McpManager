using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Controllers.Abstract;
using McpManager.Web.Portal.Dtos.Mcp;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Web.Portal.Controllers;

[Authorize(Policy = "McpServers")]
public class McpRequestsController : BaseController
{
    private readonly McpToolRequestRepository _requestRepository;
    private readonly McpServerRepository _serverRepository;

    public McpRequestsController(
        McpToolRequestRepository requestRepository,
        McpServerRepository serverRepository
    )
    {
        _requestRepository = requestRepository;
        _serverRepository = serverRepository;
    }

    public async Task<IActionResult> Index(McpRequestSearchDto filters, int page = 1)
    {
        filters ??= new McpRequestSearchDto();

        ViewData["Title"] = "Request Log";
        ViewData["Menu"] = "McpRequests";
        ViewData["Icon"] = HeroIcons.Render("document-text", size: 5);
        ViewData["Filters"] = filters;

        var pageSize = 50;

        var query = _requestRepository
            .GetAll()
            .OrderByDescending(r => r.CreationTime)
            .AsQueryable();

        if (filters.ServerId.HasValue)
        {
            query = query.Where(r => r.McpTool.McpServerId == filters.ServerId.Value);
        }

        if (filters.ToolId.HasValue)
        {
            query = query.Where(r => r.McpToolId == filters.ToolId.Value);
        }

        if (filters.Success.HasValue)
        {
            query = query.Where(r => r.Success == filters.Success.Value);
        }

        var totalCount = await query.CountAsync();
        var requests = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewData["Page"] = page;
        ViewData["TotalPages"] = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewData["Servers"] = await _serverRepository.GetAll().OrderBy(s => s.Name).ToListAsync();

        return View(requests);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        ViewData["Title"] = "Request Details";
        ViewData["Menu"] = "McpRequests";
        ViewData["Icon"] = HeroIcons.Render("document-text", size: 5);

        var request = await _requestRepository.Get(id);
        if (request == null)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(request);
    }
}
