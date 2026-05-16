using System.Data;
using AwesomeAssertions;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Repositories;

public class BaseRepositoryTransactionTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public BaseRepositoryTransactionTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task CreateTransaction_BeginsTransaction_ExposedViaHasActiveAndCurrent()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var ct = TestContext.Current.CancellationToken;

        // BaseRepository's transaction trio (CreateTransaction /
        // HasActiveTransaction / GetCurrentTransaction) was entirely zero-hit:
        // nothing exercises explicit transactions. Pin the contract multi-step
        // operations rely on — after BeginTransactionAsync the same transaction
        // must be reported as active/current; a regression returning a detached
        // transaction (or checking the wrong context) would silently break
        // atomic rollback.
        repo.HasActiveTransaction().Should().BeFalse("no transaction started yet");

        await using var tx = await repo.CreateTransaction(IsolationLevel.ReadCommitted, ct);

        repo.HasActiveTransaction().Should().BeTrue();
        repo.GetCurrentTransaction().Should().NotBeNull();
        repo.GetCurrentTransaction()!.TransactionId.Should().Be(tx.TransactionId);
    }
}
