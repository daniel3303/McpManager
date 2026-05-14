using Equibles.Core.AutoWiring;
using McpManager.Core.Data.Models.Authentication;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp.Models;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace McpManager.Core.Mcp;

[Service]
public class McpImportExportManager
{
    private readonly McpServerRepository _serverRepository;
    private readonly McpServerManager _serverManager;
    private readonly ILogger<McpImportExportManager> _logger;

    public McpImportExportManager(
        McpServerRepository serverRepository,
        McpServerManager serverManager,
        ILogger<McpImportExportManager> logger
    )
    {
        _serverRepository = serverRepository;
        _serverManager = serverManager;
        _logger = logger;
    }

    /// <summary>
    /// Import servers from Claude Desktop JSON format or mcp.json array format.
    /// Returns import result with counts.
    /// </summary>
    public async Task<ImportResult> Import(string json)
    {
        var result = new ImportResult();

        try
        {
            var parsed = JToken.Parse(json);
            var servers = ParseServers(parsed);

            foreach (var (name, config) in servers)
            {
                try
                {
                    var existingServer = await _serverRepository
                        .GetAll()
                        .FirstOrDefaultAsync(s => s.Name == name);

                    if (existingServer != null)
                    {
                        result.Skipped++;
                        result.Messages.Add($"Skipped '{name}': server already exists");
                        continue;
                    }

                    var server = BuildServerFromConfig(name, config);
                    await _serverManager.Create(server);
                    result.Imported++;
                    result.Messages.Add($"Imported '{name}'");
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    result.Messages.Add($"Error importing '{name}': {ex.Message}");
                }
            }

            result.Success = true;
        }
        catch (JsonReaderException ex)
        {
            result.Messages.Add($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Messages.Add($"Import failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Export all servers to Claude Desktop JSON format.
    /// </summary>
    public async Task<string> Export()
    {
        var servers = await _serverRepository.GetAll().ToListAsync();
        var mcpServers = new JObject();

        foreach (var server in servers)
        {
            var config = new JObject();

            if (server.TransportType == McpTransportType.Stdio)
            {
                config["command"] = server.Command;
                if (server.Arguments.Count > 0)
                {
                    config["args"] = new JArray(server.Arguments);
                }
                if (server.EnvironmentVariables.Count > 0)
                {
                    config["env"] = JObject.FromObject(server.EnvironmentVariables);
                }
            }
            else
            {
                config["url"] = server.Uri;
                if (server.TransportType == McpTransportType.Sse)
                {
                    config["transport"] = "sse";
                }
            }

            mcpServers[server.Name] = config;
        }

        var root = new JObject { ["mcpServers"] = mcpServers };
        return root.ToString(Formatting.Indented);
    }

    private List<(string Name, JObject Config)> ParseServers(JToken token)
    {
        var result = new List<(string, JObject)>();

        if (token is JObject obj)
        {
            // Claude Desktop format: { "mcpServers": { "name": { ... } } }
            if (obj["mcpServers"] is JObject mcpServers)
            {
                foreach (var prop in mcpServers.Properties())
                {
                    if (prop.Value is JObject config)
                    {
                        result.Add((prop.Name, config));
                    }
                }
            }
            // Single server object with name property
            else if (obj["name"] != null)
            {
                result.Add((obj["name"].ToString(), obj));
            }
            // Direct { "name": { config } } format
            else
            {
                foreach (var prop in obj.Properties())
                {
                    if (prop.Value is JObject config)
                    {
                        result.Add((prop.Name, config));
                    }
                }
            }
        }
        else if (token is JArray array)
        {
            // Array format: [{ "name": "...", "command": "..." }, ...]
            foreach (var item in array)
            {
                if (item is JObject serverObj && serverObj["name"] != null)
                {
                    result.Add((serverObj["name"].ToString(), serverObj));
                }
            }
        }

        return result;
    }

    private McpServer BuildServerFromConfig(string name, JObject config)
    {
        var server = new McpServer { Name = name };

        // Determine transport type
        if (config["command"] != null)
        {
            server.TransportType = McpTransportType.Stdio;
            server.Command = config["command"].ToString();
            server.Arguments = config["args"]?.ToObject<List<string>>() ?? [];
            server.EnvironmentVariables =
                config["env"]?.ToObject<Dictionary<string, string>>() ?? [];
        }
        else
        {
            var url = config["url"]?.ToString() ?? config["uri"]?.ToString();
            var transport = config["transport"]?.ToString();

            server.TransportType = string.Equals(
                transport,
                "sse",
                StringComparison.OrdinalIgnoreCase
            )
                ? McpTransportType.Sse
                : McpTransportType.Http;
            server.Uri = url;

            // Check for auth headers
            if (config["headers"] is JObject headers)
            {
                foreach (var h in headers.Properties())
                {
                    var val = h.Value.ToString();
                    if (h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        if (val.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            server.Auth.Type = AuthType.Bearer;
                            server.Auth.Token = val["Bearer ".Length..];
                        }
                        else if (val.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                        {
                            server.Auth.Type = AuthType.Basic;
                            var decoded = System.Text.Encoding.UTF8.GetString(
                                Convert.FromBase64String(val["Basic ".Length..])
                            );
                            var parts = decoded.Split(':', 2);
                            server.Auth.Username = parts[0];
                            server.Auth.Password = parts.Length > 1 ? parts[1] : "";
                        }
                    }
                    else
                    {
                        server.CustomHeaders[h.Name] = val;
                    }
                }
            }
        }

        return server;
    }
}
