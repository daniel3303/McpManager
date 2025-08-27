using System.Collections.Concurrent;
using McpManager.Core.Mcp.Models;
using Equibles.Core.AutoWiring;
using Microsoft.Extensions.DependencyInjection;

namespace McpManager.Core.Mcp;

[Service(ServiceLifetime.Singleton)]
public class InMemoryLogBuffer {
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private const int MaxEntries = 1000;

    public void Add(LogEntry entry) {
        _entries.Enqueue(entry);
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _)) { }
    }

    public List<LogEntry> GetEntries(DateTime? since = null, Guid? serverId = null, string level = null) {
        var query = _entries.AsEnumerable();

        if (since.HasValue) {
            query = query.Where(e => e.Timestamp > since.Value);
        }

        if (serverId.HasValue) {
            query = query.Where(e => e.ServerId == serverId.Value);
        }

        if (!string.IsNullOrEmpty(level)) {
            query = query.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(e => e.Timestamp).ToList();
    }
}
