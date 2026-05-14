using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Controllers.Abstract;
using McpManager.Web.Portal.Dtos.ApiKeys;
using McpManager.Web.Portal.Dtos.Users;
using McpManager.Web.Portal.Services.FlashMessage.Contracts;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpManager.Web.Portal.Controllers;

[Authorize(Policy = "ApiKeys")]
public class ApiKeysController : BaseController
{
    private readonly ApiKeyRepository _apiKeyRepository;
    private readonly ApiKeyManager _apiKeyManager;
    private readonly IFlashMessage _flashMessage;

    public ApiKeysController(
        ApiKeyRepository apiKeyRepository,
        ApiKeyManager apiKeyManager,
        IFlashMessage flashMessage
    )
    {
        _apiKeyRepository = apiKeyRepository;
        _apiKeyManager = apiKeyManager;
        _flashMessage = flashMessage;
    }

    public IActionResult Index(TextSearchDto filters)
    {
        filters ??= new TextSearchDto();
        ViewData["Title"] = "API Keys";
        ViewData["Menu"] = "ApiKeys";
        ViewData["Icon"] = HeroIcons.Render("key", size: 5);
        ViewData["Filters"] = filters;

        var query = _apiKeyRepository.GetAll();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.ToLower();
            query = query.Where(k => k.Name.ToLower().Contains(search));
        }

        query = query.OrderByDescending(k => k.CreationTime);

        return View(query);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        ViewData["Title"] = "API Key Details";
        ViewData["Menu"] = "ApiKeys";
        ViewData["Icon"] = HeroIcons.Render("key", size: 5);

        var apiKey = await _apiKeyRepository.Get(id);
        if (apiKey == null)
        {
            _flashMessage.Error("API Key not found.");
            return RedirectToAction(nameof(Index));
        }

        return View(apiKey);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Create API Key";
        ViewData["Menu"] = "ApiKeys";
        ViewData["Icon"] = HeroIcons.Render("plus", size: 5);

        return View("Form", new ApiKeyDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ApiKeyDto dto)
    {
        ViewData["Title"] = "Create API Key";
        ViewData["Menu"] = "ApiKeys";
        ViewData["Icon"] = HeroIcons.Render("plus", size: 5);

        if (!ModelState.IsValid)
        {
            return View("Form", dto);
        }

        var apiKey = new ApiKey { Name = dto.Name };

        await _apiKeyManager.Create(apiKey);

        _flashMessage.Success(
            "API Key created successfully. Copy the key now — it won't be shown again in full."
        );
        TempData["NewApiKey"] = apiKey.Key;

        return RedirectToAction(nameof(Show), new { id = apiKey.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var apiKey = await _apiKeyRepository.Get(id);
        if (apiKey == null)
            return NotFound();

        var dto = new ApiKeyDto { Name = apiKey.Name };
        return PartialView("_EditForm", dto);
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ApiKeyDto dto)
    {
        var apiKey = await _apiKeyRepository.Get(id);
        if (apiKey == null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            return PartialView("_EditForm", dto);
        }

        await _apiKeyManager.Rename(apiKey, dto.Name);
        return Json(new { Success = true, Redirect = Url.Action(nameof(Show), new { id }) });
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var apiKey = await _apiKeyRepository.Get(id);
        if (apiKey == null)
        {
            _flashMessage.Error("API Key not found.");
            return RedirectToAction(nameof(Index));
        }

        await _apiKeyManager.Delete(apiKey);

        _flashMessage.Success("API Key deleted successfully.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> RevealKey(Guid id)
    {
        var apiKey = await _apiKeyRepository.Get(id);
        if (apiKey == null)
            return Json(new { Success = false, Message = "API Key not found." });

        return Json(new { Success = true, Key = apiKey.Key });
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var apiKey = await _apiKeyRepository.Get(id);
        if (apiKey == null)
        {
            _flashMessage.Error("API Key not found.");
            return RedirectToAction(nameof(Index));
        }

        await _apiKeyManager.ToggleActive(apiKey);

        _flashMessage.Success($"API Key {(apiKey.IsActive ? "activated" : "deactivated")}.");
        return RedirectToAction(nameof(Show), new { id });
    }
}
