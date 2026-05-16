using AwesomeAssertions;
using McpManager.Core.Mcp;
using McpManager.Core.Mcp.Models;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp;

public class InMemoryLogBufferGetEntriesFilterTests
{
    [Fact]
    public void GetEntries_WithSinceServerIdAndLevelFilters_ReturnsOnlyMatchesNewestFirst()
    {
        var buffer = new InMemoryLogBuffer();
        var target = Guid.NewGuid();
        var other = Guid.NewGuid();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // old: filtered out by `since`
        buffer.Add(
            new LogEntry
            {
                Timestamp = t0,
                Level = "ERROR",
                ServerId = target,
            }
        );
        // wrong server: filtered out by serverId
        buffer.Add(
            new LogEntry
            {
                Timestamp = t0.AddMinutes(10),
                Level = "ERROR",
                ServerId = other,
            }
        );
        // wrong level: filtered out by level (case-insensitive compare path)
        buffer.Add(
            new LogEntry
            {
                Timestamp = t0.AddMinutes(11),
                Level = "INFO",
                ServerId = target,
            }
        );
        // two matches — newest must come first
        buffer.Add(
            new LogEntry
            {
                Timestamp = t0.AddMinutes(12),
                Level = "error",
                ServerId = target,
            }
        );
        buffer.Add(
            new LogEntry
            {
                Timestamp = t0.AddMinutes(13),
                Level = "ERROR",
                ServerId = target,
            }
        );

        // Every Where branch (since/serverId/level) plus the OrderByDescending
        // were zero-hit (the only existing test calls GetEntries() unfiltered).
        // The live-logs UI relies on all three filters narrowing correctly and
        // on newest-first ordering; a regression there would surface here.
        var result = buffer.GetEntries(since: t0.AddMinutes(5), serverId: target, level: "ERROR");

        result.Should().HaveCount(2);
        result[0].Timestamp.Should().Be(t0.AddMinutes(13));
        result[1].Timestamp.Should().Be(t0.AddMinutes(12));
    }
}
