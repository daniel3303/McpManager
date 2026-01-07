using System.Text.Json;
using McpManager.Core.Mcp;
using McpManager.Core.Mcp.Models;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Authentication;
using Equibles.Core.AutoWiring;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpManager.Web.Portal.Mcp;

[Service]
public class McpProxyServer {
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly McpServerManager _mcpServerManager;
    private readonly McpToolRepository _toolRepository;
    private readonly ILogger<McpProxyServer> _logger;

    public McpProxyServer(
        IHttpContextAccessor httpContextAccessor,
        McpServerManager mcpServerManager,
        McpToolRepository toolRepository,
        ILogger<McpProxyServer> logger
    ) {
        _httpContextAccessor = httpContextAccessor;
        _mcpServerManager = mcpServerManager;
        _toolRepository = toolRepository;
        _logger = logger;
    }

    public async ValueTask<ListToolsResult> ListToolsHandler(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken
    ) {
        var allTools = await _toolRepository.GetAll()
            .Where(t => t.McpServer.IsActive)
            .ToListAsync(cancellationToken);

        var tools = allTools.Select(t => new Tool {
            Name = t.Name,
            Description = t.CustomDescription ?? t.Description,
            InputSchema = McpProxyHelpers.ParseInputSchema(t.CustomInputSchema ?? t.InputSchema)
        }).ToList();

        _logger.LogDebug("Listed {ToolCount} tools from all active servers", tools.Count);

        return new ListToolsResult { Tools = tools };
    }

    public async ValueTask<CallToolResult> CallToolHandler(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken
    ) {
        var toolName = request.Params?.Name;
        if (string.IsNullOrEmpty(toolName)) {
            throw new McpProtocolException("Tool name is required", McpErrorCode.InvalidParams);
        }

        var tool = await _toolRepository.GetAll()
            .Where(t => t.McpServer.IsActive)
            .FirstOrDefaultAsync(t => t.Name == toolName, cancellationToken);

        if (tool == null) {
            throw new McpProtocolException($"Tool '{toolName}' not found", McpErrorCode.InvalidRequest);
        }

        var apiKeyName = GetApiKeyName();
        var arguments = McpProxyHelpers.ConvertArguments(request.Params?.Arguments);

        _logger.LogInformation("Calling tool {ToolName} on server {ServerName}", toolName, tool.McpServer.Name);

        var result = await _mcpServerManager.CallTool(tool.McpServer, toolName, arguments, apiKeyName);

        if (!result.Success) {
            return new CallToolResult {
                IsError = true,
                Content = [new TextContentBlock { Text = result.Error ?? "Tool execution failed" }]
            };
        }

        var content = result.Content.Select<ToolContent, ContentBlock>(c => c.Type switch {
            "image" => new ImageContentBlock {
                Data = Convert.FromBase64String(c.Data ?? ""),
                MimeType = c.MimeType ?? "image/png"
            },
            _ => new TextContentBlock { Text = c.Text ?? "" }
        }).ToList();

        return new CallToolResult { Content = content };
    }

    private string GetApiKeyName() {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(ApiKeyAuthHandler.ApiKeyNameItemKey, out var value) == true) {
            return value as string;
        }
        return null;
    }
}
