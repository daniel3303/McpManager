using System.Diagnostics;
using System.Text.Json;
using Equibles.Core.AutoWiring;
using McpManager.Core.Data.Models.Authentication;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Data.Models.Notifications;
using McpManager.Core.Identity;
using McpManager.Core.Mcp.Client;
using McpManager.Core.Mcp.Models;
using McpManager.Core.Mcp.OpenApi;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.Core.Mcp;

[Service]
public class McpServerManager
{
    private readonly McpServerRepository _serverRepository;
    private readonly McpToolRepository _toolRepository;
    private readonly McpToolRequestRepository _toolRequestRepository;
    private readonly McpServerLogRepository _serverLogRepository;
    private readonly McpClientFactory _clientFactory;
    private readonly OpenApiSpecParser _openApiParser;
    private readonly OpenApiToolExecutor _openApiExecutor;
    private readonly NotificationManager _notificationManager;
    private readonly McpNamespaceManager _namespaceManager;
    private readonly InMemoryLogBuffer _logBuffer;
    private readonly ILogger<McpServerManager> _logger;

    public McpServerManager(
        McpServerRepository serverRepository,
        McpToolRepository toolRepository,
        McpToolRequestRepository toolRequestRepository,
        McpServerLogRepository serverLogRepository,
        McpClientFactory clientFactory,
        OpenApiSpecParser openApiParser,
        OpenApiToolExecutor openApiExecutor,
        NotificationManager notificationManager,
        McpNamespaceManager namespaceManager,
        InMemoryLogBuffer logBuffer,
        ILogger<McpServerManager> logger
    )
    {
        _serverRepository = serverRepository;
        _toolRepository = toolRepository;
        _toolRequestRepository = toolRequestRepository;
        _serverLogRepository = serverLogRepository;
        _clientFactory = clientFactory;
        _openApiParser = openApiParser;
        _openApiExecutor = openApiExecutor;
        _notificationManager = notificationManager;
        _namespaceManager = namespaceManager;
        _logBuffer = logBuffer;
        _logger = logger;
    }

    public async Task<McpServer> Create(McpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        ValidateServer(server);
        _serverRepository.Add(server);
        await _serverRepository.SaveChanges();
        _logger.LogInformation(
            "Created MCP server {ServerName} with ID {ServerId}",
            server.Name,
            server.Id
        );
        return server;
    }

    public async Task Update(McpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        ValidateServer(server);
        await _serverRepository.SaveChanges();
        _logger.LogInformation(
            "Updated MCP server {ServerName} with ID {ServerId}",
            server.Name,
            server.Id
        );
    }

    public async Task Delete(McpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        _serverRepository.Remove(server);
        await _serverRepository.SaveChanges();
        _logger.LogInformation(
            "Deleted MCP server {ServerName} with ID {ServerId}",
            server.Name,
            server.Id
        );
    }

    public async Task<bool> CheckHealth(McpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        try
        {
            if (server.TransportType == McpTransportType.OpenApi)
            {
                var healthy = await _openApiExecutor.CheckHealth(server);
                if (!healthy)
                    throw new Exception("Server returned 5xx status");
            }
            else
            {
                await using var client = await _clientFactory.Create(server);
            }

            server.LastError = null;
            await _serverRepository.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            var endpoint =
                server.TransportType == McpTransportType.Stdio ? server.Command : server.Uri;
            var errorMessage = $"Failed to connect to {endpoint}: {ex.Message}";
            server.LastError = errorMessage;
            await _serverRepository.SaveChanges();
            await CreateServerLog(server, McpServerLogLevel.Error, errorMessage);
            await _notificationManager.CreateForAllUsers(
                $"Health check failed: {server.Name}",
                errorMessage,
                NotificationType.Error,
                icon: "x-circle"
            );
            _logger.LogWarning(ex, "Health check failed for MCP server {ServerName}", server.Name);
            return false;
        }
    }

    public async Task<SyncResult> SyncTools(McpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        var result = new SyncResult();

        try
        {
            List<ToolSyncEntry> toolEntries;

            if (server.TransportType == McpTransportType.OpenApi)
            {
                var operations = _openApiParser.ParseSpec(server.OpenApiSpecification);
                toolEntries = operations
                    .Select(op => new ToolSyncEntry
                    {
                        Name = op.Name,
                        Description = op.Description ?? "",
                        InputSchema = op.InputSchema ?? "{}",
                        Metadata = op.Metadata,
                    })
                    .ToList();
            }
            else
            {
                await using var client = await _clientFactory.Create(server);
                var remoteTools = await client.ListToolsAsync();
                toolEntries = remoteTools
                    .Select(rt => new ToolSyncEntry
                    {
                        Name = rt.Name,
                        Description = rt.Description ?? "",
                        InputSchema =
                            rt.JsonSchema.ValueKind != JsonValueKind.Undefined
                                ? rt.JsonSchema.GetRawText()
                                : "{}",
                    })
                    .ToList();
            }

            MergeTools(server, toolEntries, result);

            server.LastSyncTime = DateTime.UtcNow;
            server.LastError = null;

            await _serverRepository.SaveChanges();

            result.Success = true;

            // Sync namespace tools for all namespaces that include this server
            await _namespaceManager.SyncToolsForAllNamespaces(server);

            _logBuffer.Add(
                new LogEntry
                {
                    Level = "Info",
                    Message =
                        $"Sync completed: {result.ToolsAdded} added, {result.ToolsUpdated} updated, {result.ToolsRemoved} removed",
                    ServerId = server.Id,
                    ServerName = server.Name,
                }
            );
            await CreateServerLog(
                server,
                McpServerLogLevel.Info,
                $"Sync completed: {result.ToolsAdded} added, {result.ToolsUpdated} updated, {result.ToolsRemoved} removed"
            );
            _logger.LogInformation(
                "Synced tools for MCP server {ServerName}: {Added} added, {Updated} updated, {Removed} removed",
                server.Name,
                result.ToolsAdded,
                result.ToolsUpdated,
                result.ToolsRemoved
            );
        }
        catch (Exception ex)
        {
            var endpoint =
                server.TransportType == McpTransportType.Stdio ? server.Command : server.Uri;
            var errorMessage = $"Failed to connect to {endpoint}: {ex.Message}";
            result.Error = errorMessage;
            server.LastError = errorMessage;
            await _serverRepository.SaveChanges();
            await CreateServerLog(server, McpServerLogLevel.Error, errorMessage);
            await _notificationManager.CreateForAllUsers(
                $"Tool sync failed: {server.Name}",
                errorMessage,
                NotificationType.Error,
                icon: "x-circle"
            );
            _logger.LogWarning(ex, "Tool sync failed for MCP server {ServerName}", server.Name);
        }

        return result;
    }

    public async Task<ToolExecutionResult> CallTool(
        McpServer server,
        string toolName,
        Dictionary<string, object> arguments,
        string apiKeyName = null,
        Guid? namespaceId = null
    )
    {
        ArgumentNullException.ThrowIfNull(server);
        var result = new ToolExecutionResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (server.TransportType == McpTransportType.OpenApi)
            {
                var tool = await _toolRepository.GetByName(server, toolName).FirstOrDefaultAsync();
                if (tool == null)
                    throw new InvalidOperationException($"Tool '{toolName}' not found");

                var execResult = await _openApiExecutor.Execute(server, tool, arguments);
                result.Success = execResult.Success;
                result.Error = execResult.Error;
                result.Content = execResult.Content;
            }
            else
            {
                await using var client = await _clientFactory.Create(server);
                var callResult = await client.CallToolAsync(toolName, arguments);

                foreach (var content in callResult.Content)
                {
                    var toolContent = new ToolContent { Type = content.Type };

                    switch (content)
                    {
                        case TextContentBlock textBlock:
                            toolContent.Text = textBlock.Text;
                            break;
                        case ImageContentBlock imageBlock:
                            toolContent.Data = Convert.ToBase64String(imageBlock.Data.Span);
                            toolContent.MimeType = imageBlock.MimeType;
                            break;
                    }

                    result.Content.Add(toolContent);
                }

                result.Success = true;
            }
            _logBuffer.Add(
                new LogEntry
                {
                    Level = "Info",
                    Message =
                        $"Tool '{toolName}' executed successfully ({stopwatch.ElapsedMilliseconds}ms)",
                    ServerId = server.Id,
                    ServerName = server.Name,
                    ToolName = toolName,
                }
            );
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            await CreateServerLog(
                server,
                McpServerLogLevel.Error,
                $"Tool execution failed for {toolName}: {ex.Message}"
            );
            _logBuffer.Add(
                new LogEntry
                {
                    Level = "Error",
                    Message = $"Tool '{toolName}' failed: {ex.Message}",
                    ServerId = server.Id,
                    ServerName = server.Name,
                    ToolName = toolName,
                }
            );
            _logger.LogWarning(
                ex,
                "Tool execution failed for {ToolName} on server {ServerName}",
                toolName,
                server.Name
            );
        }

        stopwatch.Stop();
        result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

        // Record the request
        await RecordToolRequest(server, toolName, arguments, result, apiKeyName, namespaceId);

        return result;
    }

    private async Task RecordToolRequest(
        McpServer server,
        string toolName,
        Dictionary<string, object> arguments,
        ToolExecutionResult result,
        string apiKeyName,
        Guid? namespaceId = null
    )
    {
        try
        {
            var tool = await _toolRepository.GetByName(server, toolName).FirstOrDefaultAsync();
            if (tool == null)
                return;

            var request = new McpToolRequest
            {
                McpToolId = tool.Id,
                ApiKeyName = apiKeyName,
                McpNamespaceId = namespaceId,
                Parameters = JsonConvert.SerializeObject(arguments),
                Response = JsonConvert.SerializeObject(result.Content),
                Success = result.Success,
                ExecutionTimeMs = result.ExecutionTimeMs,
            };

            _toolRequestRepository.Add(request);
            tool.TotalRequests++;
            await _toolRequestRepository.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record tool request for {ToolName}", toolName);
        }
    }

    private async Task CreateServerLog(McpServer server, McpServerLogLevel level, string message)
    {
        try
        {
            var log = new McpServerLog
            {
                McpServerId = server.Id,
                Level = level,
                Message = message.Length > 4000 ? message[..4000] : message,
            };
            _serverLogRepository.Add(log);
            await _serverLogRepository.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create server log entry for {ServerName}",
                server.Name
            );
        }
    }

    private void MergeTools(McpServer server, List<ToolSyncEntry> toolEntries, SyncResult result)
    {
        var existingTools = _toolRepository.GetByServer(server).ToList();
        var existingToolsByName = existingTools.ToDictionary(t => t.Name);
        var remoteToolNames = new HashSet<string>(toolEntries.Select(t => t.Name));

        foreach (var entry in toolEntries)
        {
            if (existingToolsByName.TryGetValue(entry.Name, out var existingTool))
            {
                var needsUpdate =
                    existingTool.Description != entry.Description
                    || existingTool.InputSchema != entry.InputSchema
                    || existingTool.Metadata != entry.Metadata;

                if (needsUpdate)
                {
                    existingTool.Description = entry.Description;
                    existingTool.InputSchema = entry.InputSchema;
                    existingTool.Metadata = entry.Metadata;
                    // Preserve CustomDescription and CustomInputSchema (user overrides)
                    result.ToolsUpdated++;
                }
            }
            else
            {
                var newTool = new McpTool
                {
                    Name = entry.Name,
                    Description = entry.Description,
                    InputSchema = entry.InputSchema,
                    Metadata = entry.Metadata,
                    McpServerId = server.Id,
                };
                _toolRepository.Add(newTool);
                result.ToolsAdded++;
            }
        }

        foreach (var existingTool in existingTools)
        {
            if (!remoteToolNames.Contains(existingTool.Name))
            {
                _toolRepository.Remove(existingTool);
                result.ToolsRemoved++;
            }
        }
    }

    public async Task UpdateToolCustomization(
        McpTool tool,
        string customDescription,
        List<(string Name, string CustomDescription)> argumentOverrides
    )
    {
        ArgumentNullException.ThrowIfNull(tool);

        tool.CustomDescription = string.IsNullOrWhiteSpace(customDescription)
            ? null
            : customDescription.Trim();

        if (argumentOverrides?.Any(a => !string.IsNullOrWhiteSpace(a.CustomDescription)) == true)
        {
            try
            {
                var schema = System.Text.Json.JsonDocument.Parse(tool.InputSchema ?? "{}");
                var customSchema = Newtonsoft.Json.Linq.JObject.Parse(tool.InputSchema ?? "{}");
                var properties = customSchema["properties"] as Newtonsoft.Json.Linq.JObject;

                if (properties != null)
                {
                    foreach (
                        var arg in argumentOverrides.Where(a =>
                            !string.IsNullOrWhiteSpace(a.CustomDescription)
                        )
                    )
                    {
                        if (properties[arg.Name] is Newtonsoft.Json.Linq.JObject propObj)
                        {
                            propObj["description"] = arg.CustomDescription.Trim();
                        }
                    }
                }

                tool.CustomInputSchema = customSchema.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch
            {
                // If schema parsing fails, skip custom input schema
            }
        }
        else
        {
            tool.CustomInputSchema = null;
        }

        await _toolRepository.SaveChanges();
    }

    public async Task ApplyToolCustomizations(
        McpServer server,
        List<(string ToolName, string OriginalDescription, string CustomDescription)> customizations
    )
    {
        ArgumentNullException.ThrowIfNull(server);
        if (customizations == null)
            return;

        var hasChanges = false;
        foreach (var customization in customizations)
        {
            if (
                string.IsNullOrWhiteSpace(customization.CustomDescription)
                || customization.CustomDescription.Trim()
                    == customization.OriginalDescription?.Trim()
            )
            {
                continue;
            }

            var tool = _toolRepository.GetByName(server, customization.ToolName).FirstOrDefault();
            if (tool == null)
                continue;

            tool.CustomDescription = customization.CustomDescription.Trim();
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _toolRepository.SaveChanges();
        }
    }

    private void ValidateServer(McpServer server)
    {
        if (string.IsNullOrWhiteSpace(server.Name))
        {
            throw new ApplicationException("Server name is required", "Name");
        }

        if (server.TransportType == McpTransportType.OpenApi)
        {
            if (string.IsNullOrWhiteSpace(server.Uri))
            {
                throw new ApplicationException("Base URL is required for OpenAPI transport", "Uri");
            }

            if (
                !Uri.TryCreate(server.Uri, UriKind.Absolute, out var openApiUri)
                || (
                    openApiUri.Scheme != Uri.UriSchemeHttp
                    && openApiUri.Scheme != Uri.UriSchemeHttps
                )
            )
            {
                throw new ApplicationException("Base URL must be a valid HTTP or HTTPS URL", "Uri");
            }

            if (string.IsNullOrWhiteSpace(server.OpenApiSpecification))
            {
                throw new ApplicationException(
                    "OpenAPI specification is required",
                    "OpenApiSpecification"
                );
            }
        }
        else if (server.TransportType is McpTransportType.Http or McpTransportType.Sse)
        {
            if (string.IsNullOrWhiteSpace(server.Uri))
            {
                throw new ApplicationException("Server URI is required for HTTP transport", "Uri");
            }

            if (
                !Uri.TryCreate(server.Uri, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            )
            {
                throw new ApplicationException(
                    "Server URI must be a valid HTTP or HTTPS URL",
                    "Uri"
                );
            }

            switch (server.Auth.Type)
            {
                case AuthType.Basic:
                    if (string.IsNullOrWhiteSpace(server.Auth.Username))
                    {
                        throw new ApplicationException(
                            "Username is required for Basic authentication",
                            "Auth.Username"
                        );
                    }
                    break;

                case AuthType.Bearer:
                    if (string.IsNullOrWhiteSpace(server.Auth.Token))
                    {
                        throw new ApplicationException(
                            "Token is required for Bearer authentication",
                            "Auth.Token"
                        );
                    }
                    break;

                case AuthType.ApiKey:
                    if (string.IsNullOrWhiteSpace(server.Auth.ApiKeyName))
                    {
                        throw new ApplicationException(
                            "API key name is required for ApiKey authentication",
                            "Auth.ApiKeyName"
                        );
                    }
                    if (string.IsNullOrWhiteSpace(server.Auth.ApiKeyValue))
                    {
                        throw new ApplicationException(
                            "API key value is required for ApiKey authentication",
                            "Auth.ApiKeyValue"
                        );
                    }
                    break;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(server.Command))
            {
                throw new ApplicationException(
                    "Command is required for Stdio transport",
                    "Command"
                );
            }
        }
    }
}
