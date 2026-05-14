using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Controllers.Abstract;
using McpManager.Web.Portal.Dtos.Mcp;
using McpManager.Web.Portal.Dtos.Users;
using McpManager.Web.Portal.Services.FlashMessage.Contracts;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.Web.Portal.Controllers;

[Authorize(Policy = "McpNamespaces")]
public class McpNamespacesController : BaseController
{
    private readonly McpNamespaceRepository _namespaceRepository;
    private readonly McpNamespaceServerRepository _namespaceServerRepository;
    private readonly McpNamespaceToolRepository _namespaceToolRepository;
    private readonly McpServerRepository _serverRepository;
    private readonly McpNamespaceManager _namespaceManager;
    private readonly IFlashMessage _flashMessage;

    public McpNamespacesController(
        McpNamespaceRepository namespaceRepository,
        McpNamespaceServerRepository namespaceServerRepository,
        McpNamespaceToolRepository namespaceToolRepository,
        McpServerRepository serverRepository,
        McpNamespaceManager namespaceManager,
        IFlashMessage flashMessage
    )
    {
        _namespaceRepository = namespaceRepository;
        _namespaceServerRepository = namespaceServerRepository;
        _namespaceToolRepository = namespaceToolRepository;
        _serverRepository = serverRepository;
        _namespaceManager = namespaceManager;
        _flashMessage = flashMessage;
    }

    public IActionResult Index(TextSearchDto filters)
    {
        filters ??= new TextSearchDto();
        ViewData["Title"] = "Namespaces";
        ViewData["Menu"] = "McpNamespaces";
        ViewData["Icon"] = HeroIcons.Render("rectangle-group", size: 5);
        ViewData["Filters"] = filters;

        var query = _namespaceRepository.GetAll();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.ToLower();
            query = query.Where(n =>
                n.Name.ToLower().Contains(search) || n.Slug.ToLower().Contains(search)
            );
        }

        query = query.OrderBy(n => n.Name);

        return View(query);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        ViewData["Menu"] = "McpNamespaces";
        ViewData["Icon"] = HeroIcons.Render("rectangle-group", size: 5);

        var ns = await _namespaceRepository.Get(id);
        if (ns == null)
        {
            _flashMessage.Error("Namespace not found.");
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = ns.Name;

        // Get servers not yet in this namespace for the add dropdown
        var existingServerIds = await _namespaceServerRepository
            .GetByNamespace(ns)
            .Select(s => s.McpServerId)
            .ToListAsync();
        var availableServers = await _serverRepository
            .GetAll()
            .Where(s => !existingServerIds.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToListAsync();
        ViewData["AvailableServers"] = availableServers;
        ViewData["McpEndpoint"] = $"/mcp/ns/{ns.Slug}";

        var nsServers = await _namespaceServerRepository.GetByNamespace(ns).ToListAsync();
        ViewData["NsServers"] = nsServers;

        var nsServerIds = nsServers.Select(s => s.Id).ToList();
        var allNsTools = await _namespaceToolRepository
            .GetAll()
            .Where(t => nsServerIds.Contains(t.McpNamespaceServerId))
            .ToListAsync();
        ViewData["NsToolsByServer"] = allNsTools
            .GroupBy(t => t.McpNamespaceServerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return View(ns);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Create Namespace";
        ViewData["Menu"] = "McpNamespaces";
        ViewData["Icon"] = HeroIcons.Render("plus", size: 5);

        return View("Form", new McpNamespaceDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(McpNamespaceDto dto)
    {
        ViewData["Title"] = "Create Namespace";
        ViewData["Menu"] = "McpNamespaces";
        ViewData["Icon"] = HeroIcons.Render("plus", size: 5);

        if (!ModelState.IsValid)
        {
            return View("Form", dto);
        }

        var ns = new McpNamespace
        {
            Name = dto.Name,
            Slug = dto.Slug,
            Description = dto.Description,
            RateLimitEnabled = dto.RateLimitEnabled,
            RateLimitRequestsPerMinute = dto.RateLimitRequestsPerMinute,
            RateLimitStrategy = dto.RateLimitStrategy,
        };

        try
        {
            await _namespaceManager.Create(ns);
            _flashMessage.Success("Namespace created successfully.");
            return RedirectToAction(nameof(Show), new { id = ns.Id });
        }
        catch (ApplicationException ex)
        {
            ModelState.AddModelError(ex.Property ?? "", ex.Message);
            return View("Form", dto);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        ViewData["Menu"] = "McpNamespaces";
        ViewData["Icon"] = HeroIcons.Render("pencil-square", size: 5);

        var ns = await _namespaceRepository.Get(id);
        if (ns == null)
        {
            _flashMessage.Error("Namespace not found.");
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"Edit {ns.Name}";
        ViewData["Model"] = ns;

        var dto = new McpNamespaceDto
        {
            Name = ns.Name,
            Slug = ns.Slug,
            Description = ns.Description,
            RateLimitEnabled = ns.RateLimitEnabled,
            RateLimitRequestsPerMinute = ns.RateLimitRequestsPerMinute,
            RateLimitStrategy = ns.RateLimitStrategy,
        };

        return View("Form", dto);
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, McpNamespaceDto dto)
    {
        ViewData["Menu"] = "McpNamespaces";
        ViewData["Icon"] = HeroIcons.Render("pencil-square", size: 5);

        var ns = await _namespaceRepository.Get(id);
        if (ns == null)
        {
            _flashMessage.Error("Namespace not found.");
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"Edit {ns.Name}";
        ViewData["Model"] = ns;

        if (!ModelState.IsValid)
        {
            return View("Form", dto);
        }

        ns.Name = dto.Name;
        ns.Slug = dto.Slug;
        ns.Description = dto.Description;
        ns.RateLimitEnabled = dto.RateLimitEnabled;
        ns.RateLimitRequestsPerMinute = dto.RateLimitRequestsPerMinute;
        ns.RateLimitStrategy = dto.RateLimitStrategy;

        try
        {
            await _namespaceManager.Update(ns);
            _flashMessage.Success("Namespace updated successfully.");
            return RedirectToAction(nameof(Show), new { id });
        }
        catch (ApplicationException ex)
        {
            ModelState.AddModelError(ex.Property ?? "", ex.Message);
            return View("Form", dto);
        }
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ns = await _namespaceRepository.Get(id);
        if (ns == null)
        {
            _flashMessage.Error("Namespace not found.");
            return RedirectToAction(nameof(Index));
        }

        await _namespaceManager.Delete(ns);
        _flashMessage.Success("Namespace deleted successfully.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddServer(Guid id, Guid serverId)
    {
        var ns = await _namespaceRepository.Get(id);
        if (ns == null)
            return NotFound();

        var server = await _serverRepository.Get(serverId);
        if (server == null)
            return NotFound();

        await _namespaceManager.AddServer(ns, server);
        _flashMessage.Success($"Server '{server.Name}' added to namespace.");
        return RedirectToAction(nameof(Show), new { id });
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveServer(Guid id, Guid nsServerId)
    {
        var nsServer = await _namespaceServerRepository.Get(nsServerId);
        if (nsServer == null)
            return NotFound();

        await _namespaceManager.RemoveServer(nsServer);
        _flashMessage.Success("Server removed from namespace.");
        return RedirectToAction(nameof(Show), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleServer(Guid nsServerId, bool isActive)
    {
        var nsServer = await _namespaceServerRepository.Get(nsServerId);
        if (nsServer == null)
            return NotFound();

        await _namespaceManager.ToggleServer(nsServer, isActive);
        return Json(new { Success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTool(Guid nsToolId, bool isEnabled)
    {
        var nsTool = await _namespaceToolRepository.Get(nsToolId);
        if (nsTool == null)
            return NotFound();

        await _namespaceManager.ToggleTool(nsTool, isEnabled);
        return Json(new { Success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditToolOverride(
        Guid nsToolId,
        string nameOverride,
        string descriptionOverride
    )
    {
        var nsTool = await _namespaceToolRepository.Get(nsToolId);
        if (nsTool == null)
            return NotFound();

        await _namespaceManager.UpdateToolOverride(nsTool, nameOverride, descriptionOverride);
        return Json(new { Success = true });
    }
}
