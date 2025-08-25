using System.Text.RegularExpressions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using Equibles.Core.AutoWiring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.Core.Mcp;

[Service]
public class McpNamespaceManager {
    private readonly McpNamespaceRepository _namespaceRepository;
    private readonly McpNamespaceServerRepository _namespaceServerRepository;
    private readonly McpNamespaceToolRepository _namespaceToolRepository;
    private readonly McpToolRepository _toolRepository;
    private readonly ILogger<McpNamespaceManager> _logger;

    public McpNamespaceManager(
        McpNamespaceRepository namespaceRepository,
        McpNamespaceServerRepository namespaceServerRepository,
        McpNamespaceToolRepository namespaceToolRepository,
        McpToolRepository toolRepository,
        ILogger<McpNamespaceManager> logger
    ) {
        _namespaceRepository = namespaceRepository;
        _namespaceServerRepository = namespaceServerRepository;
        _namespaceToolRepository = namespaceToolRepository;
        _toolRepository = toolRepository;
        _logger = logger;
    }

    public async Task<McpNamespace> Create(McpNamespace ns) {
        ArgumentNullException.ThrowIfNull(ns);
        Validate(ns);
        await ValidateSlugUnique(ns);
        _namespaceRepository.Add(ns);
        await _namespaceRepository.SaveChanges();
        _logger.LogInformation("Created namespace {Name} ({Slug})", ns.Name, ns.Slug);
        return ns;
    }

    public async Task Update(McpNamespace ns) {
        ArgumentNullException.ThrowIfNull(ns);
        Validate(ns);
        await ValidateSlugUnique(ns);
        await _namespaceRepository.SaveChanges();
        _logger.LogInformation("Updated namespace {Name} ({Slug})", ns.Name, ns.Slug);
    }

    public async Task Delete(McpNamespace ns) {
        ArgumentNullException.ThrowIfNull(ns);
        _namespaceRepository.Remove(ns);
        await _namespaceRepository.SaveChanges();
        _logger.LogInformation("Deleted namespace {Name} ({Slug})", ns.Name, ns.Slug);
    }

    public async Task<McpNamespaceServer> AddServer(McpNamespace ns, McpServer server) {
        var existing = await _namespaceServerRepository.GetByNamespace(ns)
            .FirstOrDefaultAsync(s => s.McpServerId == server.Id);

        if (existing != null) {
            return existing;
        }

        var nsServer = new McpNamespaceServer {
            McpNamespaceId = ns.Id,
            McpServerId = server.Id
        };
        _namespaceServerRepository.Add(nsServer);
        await _namespaceServerRepository.SaveChanges();

        // Sync tools for this server in the namespace
        await SyncToolsForServer(nsServer);

        _logger.LogInformation("Added server {ServerName} to namespace {NsName}", server.Name, ns.Name);
        return nsServer;
    }

    public async Task RemoveServer(McpNamespaceServer nsServer) {
        _namespaceServerRepository.Remove(nsServer);
        await _namespaceServerRepository.SaveChanges();
    }

    public async Task ToggleServer(McpNamespaceServer nsServer, bool isActive) {
        nsServer.IsActive = isActive;
        await _namespaceServerRepository.SaveChanges();
    }

    public async Task ToggleTool(McpNamespaceTool nsTool, bool isEnabled) {
        nsTool.IsEnabled = isEnabled;
        await _namespaceToolRepository.SaveChanges();
    }

    public async Task UpdateToolOverride(McpNamespaceTool nsTool, string nameOverride, string descriptionOverride) {
        nsTool.NameOverride = string.IsNullOrWhiteSpace(nameOverride) ? null : nameOverride.Trim();
        nsTool.DescriptionOverride = string.IsNullOrWhiteSpace(descriptionOverride) ? null : descriptionOverride.Trim();
        await _namespaceToolRepository.SaveChanges();
    }

    /// <summary>
    /// Ensures McpNamespaceTool rows exist for all tools on a given namespace server.
    /// Called after a server tool sync completes.
    /// </summary>
    public async Task SyncToolsForServer(McpNamespaceServer nsServer) {
        var serverTools = await _toolRepository.GetByServer(nsServer.McpServer).ToListAsync();
        var existingNsTools = await _namespaceToolRepository.GetByNamespaceServer(nsServer).ToListAsync();
        var existingToolIds = existingNsTools.Select(t => t.McpToolId).ToHashSet();

        foreach (var tool in serverTools) {
            if (!existingToolIds.Contains(tool.Id)) {
                _namespaceToolRepository.Add(new McpNamespaceTool {
                    McpNamespaceServerId = nsServer.Id,
                    McpToolId = tool.Id
                });
            }
        }

        // Remove namespace tools that no longer exist on the server
        var serverToolIds = serverTools.Select(t => t.Id).ToHashSet();
        var toRemove = existingNsTools.Where(t => !serverToolIds.Contains(t.McpToolId)).ToList();
        if (toRemove.Count > 0) {
            _namespaceToolRepository.Remove(toRemove);
        }

        await _namespaceToolRepository.SaveChanges();
    }

    /// <summary>
    /// Syncs namespace tools for all namespaces that include the given server.
    /// Called from McpServerManager.SyncTools after a server sync completes.
    /// </summary>
    public async Task SyncToolsForAllNamespaces(McpServer server) {
        var nsServers = await _namespaceServerRepository.GetByServer(server).ToListAsync();
        foreach (var nsServer in nsServers) {
            await SyncToolsForServer(nsServer);
        }
    }

    private void Validate(McpNamespace ns) {
        if (string.IsNullOrWhiteSpace(ns.Name)) {
            throw new ApplicationException("Name is required", "Name");
        }

        if (string.IsNullOrWhiteSpace(ns.Slug)) {
            throw new ApplicationException("Slug is required", "Slug");
        }

        if (!Regex.IsMatch(ns.Slug, @"^[a-z0-9][a-z0-9-]*$")) {
            throw new ApplicationException("Slug must contain only lowercase letters, numbers, and hyphens, and start with a letter or number", "Slug");
        }
    }

    private async Task ValidateSlugUnique(McpNamespace ns) {
        var existing = await _namespaceRepository.GetBySlug(ns.Slug)
            .FirstOrDefaultAsync(n => n.Id != ns.Id);

        if (existing != null) {
            throw new ApplicationException("A namespace with this slug already exists", "Slug");
        }
    }
}
