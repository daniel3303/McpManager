using System.Text.Json;
using Equibles.Core.AutoWiring;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Mcp.Models;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Authentication;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpManager.Web.Portal.Mcp;

[Service]
public class McpNamespaceProxyServer
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly McpServerManager _mcpServerManager;
    private readonly McpNamespaceRepository _namespaceRepository;
    private readonly McpNamespaceServerRepository _namespaceServerRepository;
    private readonly McpNamespaceToolRepository _namespaceToolRepository;
    private readonly McpToolRepository _toolRepository;
    private readonly McpToolRequestRepository _toolRequestRepository;
    private readonly ILogger<McpNamespaceProxyServer> _logger;

    public McpNamespaceProxyServer(
        IHttpContextAccessor httpContextAccessor,
        McpServerManager mcpServerManager,
        McpNamespaceRepository namespaceRepository,
        McpNamespaceServerRepository namespaceServerRepository,
        McpNamespaceToolRepository namespaceToolRepository,
        McpToolRepository toolRepository,
        McpToolRequestRepository toolRequestRepository,
        ILogger<McpNamespaceProxyServer> logger
    )
    {
        _httpContextAccessor = httpContextAccessor;
        _mcpServerManager = mcpServerManager;
        _namespaceRepository = namespaceRepository;
        _namespaceServerRepository = namespaceServerRepository;
        _namespaceToolRepository = namespaceToolRepository;
        _toolRepository = toolRepository;
        _toolRequestRepository = toolRequestRepository;
        _logger = logger;
    }

    public async ValueTask<ListToolsResult> ListToolsHandler(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken
    )
    {
        var ns = await GetNamespaceFromRoute();
        if (ns == null)
        {
            return new ListToolsResult { Tools = [] };
        }

        var nsTools = await GetEnabledNamespaceTools(ns, cancellationToken);

        var tools = nsTools
            .Select(nst => new Tool
            {
                Name = nst.NameOverride ?? nst.McpTool.Name,
                Description =
                    nst.DescriptionOverride
                    ?? nst.McpTool.CustomDescription
                    ?? nst.McpTool.Description,
                InputSchema = McpProxyHelpers.ParseInputSchema(
                    nst.McpTool.CustomInputSchema ?? nst.McpTool.InputSchema
                ),
            })
            .ToList();

        _logger.LogDebug("Listed {ToolCount} tools from namespace {NsSlug}", tools.Count, ns.Slug);

        return new ListToolsResult { Tools = tools };
    }

    public async ValueTask<CallToolResult> CallToolHandler(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken
    )
    {
        var toolName = request.Params?.Name;
        if (string.IsNullOrEmpty(toolName))
        {
            throw new McpProtocolException("Tool name is required", McpErrorCode.InvalidParams);
        }

        var ns = await GetNamespaceFromRoute();
        if (ns == null)
        {
            throw new McpProtocolException("Namespace not found", McpErrorCode.InvalidRequest);
        }

        var nsTools = await GetEnabledNamespaceTools(ns, cancellationToken);

        // Find tool by effective name (override name or original name)
        var nsTool = nsTools.FirstOrDefault(t => (t.NameOverride ?? t.McpTool.Name) == toolName);

        if (nsTool == null)
        {
            throw new McpProtocolException(
                $"Tool '{toolName}' not found in namespace '{ns.Slug}'",
                McpErrorCode.InvalidRequest
            );
        }

        var server = nsTool.McpNamespaceServer.McpServer;
        var originalToolName = nsTool.McpTool.Name;
        var apiKeyName = GetApiKeyName();
        var arguments = McpProxyHelpers.ConvertArguments(request.Params?.Arguments);

        _logger.LogInformation(
            "Calling tool {ToolName} (original: {OriginalName}) on server {ServerName} via namespace {NsSlug}",
            toolName,
            originalToolName,
            server.Name,
            ns.Slug
        );

        var result = await _mcpServerManager.CallTool(
            server,
            originalToolName,
            arguments,
            apiKeyName,
            ns.Id
        );

        if (!result.Success)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = result.Error ?? "Tool execution failed" }],
            };
        }

        var content = result
            .Content.Select<ToolContent, ContentBlock>(c =>
                c.Type switch
                {
                    "image" => new ImageContentBlock
                    {
                        Data = Convert.FromBase64String(c.Data ?? ""),
                        MimeType = c.MimeType ?? "image/png",
                    },
                    _ => new TextContentBlock { Text = c.Text ?? "" },
                }
            )
            .ToList();

        return new CallToolResult { Content = content };
    }

    private async Task<McpNamespace> GetNamespaceFromRoute()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var slug = httpContext?.Request.RouteValues["slug"] as string;
        if (string.IsNullOrEmpty(slug))
            return null;

        return await _namespaceRepository.GetBySlug(slug).FirstOrDefaultAsync();
    }

    private async Task<List<McpNamespaceTool>> GetEnabledNamespaceTools(
        McpNamespace ns,
        CancellationToken cancellationToken
    )
    {
        return await _namespaceToolRepository
            .GetAll()
            .Where(t =>
                t.McpNamespaceServer.McpNamespaceId == ns.Id
                && t.McpNamespaceServer.IsActive
                && t.McpNamespaceServer.McpServer.IsActive
                && t.IsEnabled
            )
            .ToListAsync(cancellationToken);
    }

    private string GetApiKeyName()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (
            httpContext?.Items.TryGetValue(ApiKeyAuthHandler.ApiKeyNameItemKey, out var value)
            == true
        )
        {
            return value as string;
        }
        return null;
    }
}
