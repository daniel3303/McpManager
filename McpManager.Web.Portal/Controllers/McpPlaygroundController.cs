using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace McpManager.Web.Portal.Controllers;

[Authorize(Policy = "McpServers")]
public class McpPlaygroundController : BaseController {
    private readonly McpServerRepository _mcpServerRepository;
    private readonly McpToolRepository _mcpToolRepository;
    private readonly McpServerManager _mcpServerManager;

    public McpPlaygroundController(
        McpServerRepository mcpServerRepository,
        McpToolRepository mcpToolRepository,
        McpServerManager mcpServerManager
    ) {
        _mcpServerRepository = mcpServerRepository;
        _mcpToolRepository = mcpToolRepository;
        _mcpServerManager = mcpServerManager;
    }

    public IActionResult Index() {
        ViewData["Title"] = "MCP Playground";
        ViewData["Menu"] = "McpPlayground";
        ViewData["Icon"] = HeroIcons.Render("command-line", size: 5);

        var servers = _mcpServerRepository.GetAll()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
            .ToList();

        ViewData["Servers"] = servers;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetTools(Guid serverId) {
        var server = await _mcpServerRepository.Get(serverId);
        if (server == null) {
            return NotFound();
        }

        var tools = await _mcpToolRepository.GetByServer(server)
            .OrderBy(t => t.Name)
            .Select(t => new ToolListItemDto {
                Id = t.Id,
                Name = t.Name,
                Description = t.CustomDescription ?? t.Description
            })
            .ToListAsync();

        return Json(tools);
    }

    [HttpGet]
    public async Task<IActionResult> GetToolForm(Guid toolId) {
        var tool = await _mcpToolRepository.Get(toolId);
        if (tool == null) {
            return NotFound();
        }

        var dto = ParseToolSchema(tool);
        return PartialView("_ToolForm", dto);
    }

    [HttpPost]
    public async Task<IActionResult> Execute([FromBody] ExecuteToolDto dto) {
        var server = await _mcpServerRepository.Get(dto.ServerId);
        if (server == null) {
            return NotFound(new { Success = false, Error = "Server not found" });
        }

        var result = await _mcpServerManager.CallTool(server, dto.ToolName, dto.Arguments);
        return Json(result);
    }

    private ToolFormDto ParseToolSchema(McpTool tool) {
        var dto = new ToolFormDto {
            ToolId = tool.Id,
            ToolName = tool.Name,
            Description = tool.Description
        };

        try {
            var schema = JObject.Parse(tool.InputSchema);
            var properties = schema["properties"] as JObject;
            var required = schema["required"] as JArray;
            var requiredNames = required?.Select(r => r.ToString()).ToHashSet() ?? [];

            if (properties == null) {
                return dto;
            }

            foreach (var prop in properties.Properties()) {
                var propValue = prop.Value as JObject;
                if (propValue == null) continue;

                var field = new ToolFormFieldDto {
                    Name = prop.Name,
                    Type = propValue["type"]?.ToString() ?? "string",
                    Description = propValue["description"]?.ToString() ?? "",
                    Required = requiredNames.Contains(prop.Name)
                };

                if (propValue["default"] != null) {
                    var defaultVal = propValue["default"];
                    field.Default = defaultVal.Type == JTokenType.Object || defaultVal.Type == JTokenType.Array
                        ? defaultVal.ToString(Newtonsoft.Json.Formatting.None)
                        : defaultVal.ToString();
                }

                if (propValue["enum"] is JArray enumArray) {
                    field.EnumValues = enumArray.Select(e => e.ToString()).ToList();
                }

                if (propValue["minimum"] != null && int.TryParse(propValue["minimum"].ToString(), out var min)) {
                    field.Minimum = min;
                }

                if (propValue["maximum"] != null && int.TryParse(propValue["maximum"].ToString(), out var max)) {
                    field.Maximum = max;
                }

                dto.Fields.Add(field);
            }
        }
        catch {
            // Invalid JSON schema, return empty fields
        }

        return dto;
    }
}
