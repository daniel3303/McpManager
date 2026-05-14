using McpManager.Core.Data.Models.Authentication;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Mcp.OpenApi;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Controllers.Abstract;
using McpManager.Web.Portal.Dtos;
using McpManager.Web.Portal.Dtos.Mcp;
using McpManager.Web.Portal.Dtos.Users;
using McpManager.Web.Portal.Services;
using McpManager.Web.Portal.Services.FlashMessage.Contracts;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.Web.Portal.Controllers;

[Authorize(Policy = "McpServers")]
public class McpServersController : BaseController
{
    private readonly McpServerRepository _mcpServerRepository;
    private readonly McpToolRepository _mcpToolRepository;
    private readonly McpServerLogRepository _mcpServerLogRepository;
    private readonly McpServerManager _mcpServerManager;
    private readonly McpImportExportManager _importExportManager;
    private readonly OpenApiSpecParser _openApiParser;
    private readonly TokenCounterService _tokenCounter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFlashMessage _flashMessage;

    public McpServersController(
        McpServerRepository mcpServerRepository,
        McpToolRepository mcpToolRepository,
        McpServerLogRepository mcpServerLogRepository,
        McpServerManager mcpServerManager,
        McpImportExportManager importExportManager,
        OpenApiSpecParser openApiParser,
        TokenCounterService tokenCounter,
        IHttpClientFactory httpClientFactory,
        IFlashMessage flashMessage
    )
    {
        _mcpServerRepository = mcpServerRepository;
        _mcpToolRepository = mcpToolRepository;
        _mcpServerLogRepository = mcpServerLogRepository;
        _mcpServerManager = mcpServerManager;
        _importExportManager = importExportManager;
        _openApiParser = openApiParser;
        _tokenCounter = tokenCounter;
        _httpClientFactory = httpClientFactory;
        _flashMessage = flashMessage;
    }

    public IActionResult Index(TextSearchDto filters)
    {
        filters ??= new TextSearchDto();
        ViewData["Title"] = "MCP Servers";
        ViewData["Menu"] = "McpServers";
        ViewData["Icon"] = HeroIcons.Render("server-stack", size: 5);
        ViewData["Filters"] = filters;

        var query = _mcpServerRepository.GetAll();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(search) || s.Description.ToLower().Contains(search)
            );
        }

        query = query.OrderBy(s => s.Name);

        return View(query);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        ViewData["Title"] = "MCP Server Details";
        ViewData["Menu"] = "McpServers";
        ViewData["Icon"] = HeroIcons.Render("server", size: 5);

        var server = await _mcpServerRepository.Get(id);

        if (server == null)
        {
            _flashMessage.Error("MCP Server not found.");
            return RedirectToAction(nameof(Index));
        }

        ViewData["RecentLogs"] = await _mcpServerLogRepository
            .GetByServer(server)
            .Take(20)
            .ToListAsync();

        return View(server);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Create MCP Server";
        ViewData["Menu"] = "McpServers";
        ViewData["Icon"] = HeroIcons.Render("plus", size: 5);

        return View("Form", new McpServerDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(McpServerDto dto)
    {
        ViewData["Title"] = "Create MCP Server";
        ViewData["Menu"] = "McpServers";
        ViewData["Icon"] = HeroIcons.Render("plus", size: 5);

        dto.CustomHeaders = dto
            .CustomHeaders.Where(h => !string.IsNullOrWhiteSpace(h.Key))
            .ToList();
        dto.EnvVars = dto.EnvVars.Where(h => !string.IsNullOrWhiteSpace(h.Key)).ToList();

        if (!ModelState.IsValid)
        {
            return View("Form", dto);
        }

        var server = MapDtoToServer(new McpServer(), dto);

        try
        {
            await _mcpServerManager.Create(server);
        }
        catch (ApplicationException ex)
        {
            ModelState.AddModelError(ex.Property ?? "", ex.Message);
            return View("Form", dto);
        }

        var syncResult = await _mcpServerManager.SyncTools(server);
        if (syncResult.Success)
        {
            await ApplyToolCustomizations(server, dto.ToolCustomizations);
            _flashMessage.Success(
                $"MCP Server created and synced successfully. {syncResult.ToolsAdded} tools found."
            );
        }
        else
        {
            _flashMessage.Warning($"MCP Server created but sync failed: {syncResult.Error}");
        }

        return RedirectToAction(nameof(Show), new { id = server.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        ViewData["Menu"] = "McpServers";
        ViewData["Icon"] = HeroIcons.Render("pencil-square", size: 5);

        var server = await _mcpServerRepository.Get(id);
        if (server == null)
        {
            _flashMessage.Error("MCP Server not found.");
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"Edit {server.Name}";
        ViewData["Model"] = server;

        var dto = MapServerToDto(server);

        return View("Form", dto);
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, McpServerDto dto)
    {
        ViewData["Menu"] = "McpServers";
        ViewData["Icon"] = HeroIcons.Render("pencil-square", size: 5);

        var server = await _mcpServerRepository.Get(id);
        if (server == null)
        {
            _flashMessage.Error("MCP Server not found.");
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"Edit {server.Name}";
        ViewData["Model"] = server;

        dto.CustomHeaders = dto
            .CustomHeaders.Where(h => !string.IsNullOrWhiteSpace(h.Key))
            .ToList();
        dto.EnvVars = dto.EnvVars.Where(h => !string.IsNullOrWhiteSpace(h.Key)).ToList();

        if (!ModelState.IsValid)
        {
            return View("Form", dto);
        }

        MapDtoToServer(server, dto);

        try
        {
            await _mcpServerManager.Update(server);
        }
        catch (ApplicationException ex)
        {
            ModelState.AddModelError(ex.Property ?? "", ex.Message);
            return View("Form", dto);
        }

        var syncResult = await _mcpServerManager.SyncTools(server);
        if (syncResult.Success)
        {
            await ApplyToolCustomizations(server, dto.ToolCustomizations);
            _flashMessage.Success(
                $"MCP Server updated and synced. {syncResult.ToolsAdded} added, {syncResult.ToolsUpdated} updated, {syncResult.ToolsRemoved} removed."
            );
        }
        else
        {
            _flashMessage.Warning($"MCP Server updated but sync failed: {syncResult.Error}");
        }

        return RedirectToAction(nameof(Show), new { id });
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var server = await _mcpServerRepository.Get(id);
        if (server == null)
        {
            _flashMessage.Error("MCP Server not found.");
            return RedirectToAction(nameof(Index));
        }

        await _mcpServerManager.Delete(server);

        _flashMessage.Success("MCP Server deleted successfully.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddHeader(McpServerDto dto)
    {
        ModelState.Clear();
        dto.CustomHeaders.Add(new CustomHeaderDto());
        return PartialView("_CustomHeadersForm", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveHeader(McpServerDto dto, int index)
    {
        ModelState.Clear();
        if (index >= 0 && index < dto.CustomHeaders.Count)
        {
            dto.CustomHeaders.RemoveAt(index);
        }
        return PartialView("_CustomHeadersForm", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateTransportFields(McpServerDto dto)
    {
        ModelState.Clear();
        return PartialView("_TransportFields", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateAuthFields(McpServerDto dto)
    {
        ModelState.Clear();
        return PartialView("_AuthForm", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateHelpPanel(McpServerDto dto)
    {
        ModelState.Clear();
        return PartialView("_HelpPanel", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateStdioMode(McpServerDto dto)
    {
        ModelState.Clear();
        return PartialView("_StdioFields", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddEnvVar(McpServerDto dto)
    {
        ModelState.Clear();
        dto.EnvVars.Add(new CustomHeaderDto());
        return PartialView("_EnvVarsForm", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveEnvVar(McpServerDto dto, int index)
    {
        ModelState.Clear();
        if (index >= 0 && index < dto.EnvVars.Count)
        {
            dto.EnvVars.RemoveAt(index);
        }
        return PartialView("_EnvVarsForm", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PreviewCommand(McpServerDto dto)
    {
        var (command, arguments) = dto.UseAdvancedCommand
            ? McpCommandBuilder.BuildCustomCommand(dto.Command, dto.ArgumentsText)
            : McpCommandBuilder.BuildNpxCommand(dto.NpmPackage, dto.ExtraArguments);
        var preview = McpCommandBuilder.BuildCommandPreview(command, arguments);
        return Json(new { Preview = preview });
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(Guid id)
    {
        var server = await _mcpServerRepository.Get(id);
        if (server == null)
        {
            _flashMessage.Error("MCP Server not found.");
            return RedirectToAction(nameof(Index));
        }

        var syncResult = await _mcpServerManager.SyncTools(server);
        if (syncResult.Success)
        {
            var message =
                $"Sync completed: {syncResult.ToolsAdded} added, {syncResult.ToolsUpdated} updated, {syncResult.ToolsRemoved} removed.";
            _flashMessage.Success(message);
        }
        else
        {
            _flashMessage.Error($"Sync failed: {syncResult.Error}");
        }

        return RedirectToAction(nameof(Show), new { id });
    }

    [HttpGet]
    public IActionResult Import()
    {
        ViewData["Title"] = "Import Servers";
        ViewData["Menu"] = "McpServers";
        ViewData["Icon"] = HeroIcons.Render("arrow-down-tray", size: 5);

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(string json)
    {
        ViewData["Title"] = "Import Servers";
        ViewData["Menu"] = "McpServers";
        ViewData["Icon"] = HeroIcons.Render("arrow-down-tray", size: 5);

        if (string.IsNullOrWhiteSpace(json))
        {
            ModelState.AddModelError("", "Please provide JSON to import.");
            return View();
        }

        var result = await _importExportManager.Import(json);

        if (result.Success)
        {
            _flashMessage.Success(
                $"Import complete: {result.Imported} imported, {result.Skipped} skipped, {result.Errors} errors."
            );
        }
        else
        {
            _flashMessage.Error("Import failed: " + string.Join("; ", result.Messages));
        }

        ViewData["ImportResult"] = result;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Export()
    {
        var json = await _importExportManager.Export();
        return File(
            System.Text.Encoding.UTF8.GetBytes(json),
            "application/json",
            "mcpmanager-servers.json"
        );
    }

    [HttpGet("{id:guid}/{toolId:guid}")]
    public async Task<IActionResult> EditTool(Guid id, Guid toolId)
    {
        var tool = await _mcpToolRepository.Get(toolId);
        if (tool == null)
            return NotFound();

        var dto = new McpToolEditDto { CustomDescription = tool.CustomDescription };

        // Parse input schema to extract argument names and descriptions
        var schemaSource = tool.InputSchema ?? "{}";
        var customSchemaSource = tool.CustomInputSchema;
        try
        {
            var schema = JObject.Parse(schemaSource);
            var customSchema = !string.IsNullOrEmpty(customSchemaSource)
                ? JObject.Parse(customSchemaSource)
                : null;
            var properties = schema["properties"] as JObject;
            var customProperties = customSchema?["properties"] as JObject;

            if (properties != null)
            {
                foreach (var prop in properties.Properties())
                {
                    var propObj = prop.Value as JObject;
                    var customPropObj = customProperties?[prop.Name] as JObject;
                    dto.Arguments.Add(
                        new McpToolArgumentDto
                        {
                            Name = prop.Name,
                            OriginalDescription = propObj?["description"]?.ToString(),
                            CustomDescription = customPropObj?["description"]?.ToString(),
                        }
                    );
                }
            }
        }
        catch
        {
            // Invalid schema, show empty arguments
        }

        ViewData["Tool"] = tool;
        return PartialView("_EditToolForm", dto);
    }

    [HttpPost("{id:guid}/{toolId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTool(Guid id, Guid toolId, McpToolEditDto dto)
    {
        var tool = await _mcpToolRepository.Get(toolId);
        if (tool == null)
            return NotFound();

        var argumentOverrides = dto.Arguments?.Select(a => (a.Name, a.CustomDescription)).ToList();

        await _mcpServerManager.UpdateToolCustomization(
            tool,
            dto.CustomDescription,
            argumentOverrides
        );

        return Json(new { Success = true, Redirect = Url.Action(nameof(Show), new { id }) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PreviewOpenApiTools(string openApiSpecification)
    {
        if (string.IsNullOrWhiteSpace(openApiSpecification))
        {
            return Json(new { Success = false, Error = "No specification provided" });
        }

        try
        {
            var operations = _openApiParser.ParseSpec(openApiSpecification);
            var tools = operations.Select(op => new { op.Name, op.Description }).ToList();
            return Json(new { Success = true, Tools = tools });
        }
        catch (Exception ex)
        {
            return Json(new { Success = false, Error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CountTokens([FromBody] CountTokensRequest request)
    {
        var count = _tokenCounter.CountTokens(request?.Text);
        return Json(new { Count = count });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FetchOpenApiSpec(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Json(new { Success = false, Error = "URL is required" });
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            var content = await httpClient.GetStringAsync(url);
            return Json(new { Success = true, Content = content });
        }
        catch (Exception ex)
        {
            return Json(new { Success = false, Error = ex.Message });
        }
    }

    private McpServer MapDtoToServer(McpServer server, McpServerDto dto)
    {
        server.Name = dto.Name;
        server.Description = dto.Description;
        server.TransportType = dto.TransportType.Value;
        server.Uri = dto.Uri;
        server.Auth.Type = dto.Auth.Type;
        server.Auth.Username = dto.Auth.Username;
        server.Auth.Password = dto.Auth.Password;
        server.Auth.Token = dto.Auth.Token;
        server.Auth.ApiKeyName = dto.Auth.ApiKeyName;
        server.Auth.ApiKeyValue = dto.Auth.ApiKeyValue;
        server.CustomHeaders = dto.CustomHeaders.ToDictionary(h => h.Key, h => h.Value);

        if (dto.TransportType == McpTransportType.Stdio)
        {
            var (cmd, args) = dto.UseAdvancedCommand
                ? McpCommandBuilder.BuildCustomCommand(dto.Command, dto.ArgumentsText)
                : McpCommandBuilder.BuildNpxCommand(dto.NpmPackage, dto.ExtraArguments);
            server.Command = cmd;
            server.Arguments = args;
        }
        else
        {
            var (cmd, args) = McpCommandBuilder.BuildCustomCommand(dto.Command, dto.ArgumentsText);
            server.Command = cmd;
            server.Arguments = args;
        }

        server.EnvironmentVariables = dto.EnvVars.ToDictionary(h => h.Key, h => h.Value);
        server.OpenApiSpecification = dto.OpenApiSpecification;
        return server;
    }

    private async Task ApplyToolCustomizations(
        McpServer server,
        List<McpToolCustomizationDto> customizations
    )
    {
        if (customizations == null)
            return;

        var mapped = customizations
            .Select(c => (c.ToolName, c.OriginalDescription, c.CustomDescription))
            .ToList();

        await _mcpServerManager.ApplyToolCustomizations(server, mapped);
    }

    private McpServerDto MapServerToDto(McpServer server)
    {
        var dto = new McpServerDto
        {
            Name = server.Name,
            Description = server.Description,
            TransportType = server.TransportType,
            Uri = server.Uri,
            Auth = new AuthDto
            {
                Type = server.Auth.Type,
                Username = server.Auth.Username,
                Password = server.Auth.Password,
                Token = server.Auth.Token,
                ApiKeyName = server.Auth.ApiKeyName,
                ApiKeyValue = server.Auth.ApiKeyValue,
            },
            CustomHeaders = server
                .CustomHeaders.Select(kvp => new CustomHeaderDto
                {
                    Key = kvp.Key,
                    Value = kvp.Value,
                })
                .ToList(),
            EnvVars = server
                .EnvironmentVariables.Select(kvp => new CustomHeaderDto
                {
                    Key = kvp.Key,
                    Value = kvp.Value,
                })
                .ToList(),
            OpenApiSpecification = server.OpenApiSpecification,
        };

        if (
            server.TransportType == McpTransportType.Stdio
            && string.Equals(server.Command, "npx", StringComparison.OrdinalIgnoreCase)
            && server.Arguments.Count >= 2
            && server.Arguments[0] == "-y"
        )
        {
            dto.NpmPackage = server.Arguments[1];
            dto.ExtraArguments =
                server.Arguments.Count > 2 ? string.Join("\n", server.Arguments.Skip(2)) : null;
            dto.UseAdvancedCommand = false;
        }
        else
        {
            dto.Command = server.Command;
            dto.ArgumentsText =
                server.Arguments.Count > 0 ? string.Join("\n", server.Arguments) : null;
            dto.UseAdvancedCommand = true;
        }

        return dto;
    }
}
