using AwesomeAssertions;
using McpManager.Core.Mcp;
using McpManager.Core.Mcp.Models;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp;

public class InMemoryLogBufferTests
{
    [Fact]
    public void Add_BeyondMaxCapacity_EvictsOldestEntries()
    {
        var buffer = new InMemoryLogBuffer();
        // MaxEntries is 1000; push 1100 distinct entries to force eviction.
        // A regression that drops the dequeue loop would leak memory in
        // long-running deployments where every upstream MCP call adds a log.
        for (var i = 0; i < 1100; i++)
        {
            buffer.Add(
                new LogEntry
                {
                    Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i),
                    Level = "INFO",
                    Message = $"entry-{i}",
                }
            );
        }

        var entries = buffer.GetEntries();

        entries.Should().HaveCount(1000);
        // The first 100 entries (entry-0 .. entry-99) must have been evicted.
        entries.Should().NotContain(e => e.Message == "entry-0");
        entries.Should().NotContain(e => e.Message == "entry-99");
        // The last 1000 entries (entry-100 .. entry-1099) must still be present.
        entries.Should().Contain(e => e.Message == "entry-100");
        entries.Should().Contain(e => e.Message == "entry-1099");
    }
}
