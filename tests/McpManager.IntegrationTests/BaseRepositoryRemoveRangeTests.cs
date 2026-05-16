using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class BaseRepositoryRemoveRangeTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public BaseRepositoryRemoveRangeTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task RemoveRange_DeletesEveryEntityInTheSequence()
    {
        var ct = TestContext.Current.CancellationToken;
        var tag = $"rmrange-{Guid.NewGuid():N}";

        Guid id1,
            id2;
        using (var scope = _factory.Services.CreateScope())
        {
            var mgr = scope.ServiceProvider.GetRequiredService<ApiKeyManager>();
            var a = await mgr.Create(new ApiKey { Name = $"{tag}-a" });
            var b = await mgr.Create(new ApiKey { Name = $"{tag}-b" });
            id1 = a.Id;
            id2 = b.Id;
        }

        // BaseRepository.Remove(IEnumerable<T>) (the loop delegating to the
        // single-entity Remove) was zero-hit — callers only ever remove one
        // entity. A regression that swapped it for a no-op or RemoveRange
        // bypassing per-entity Remove would silently skip validation hooks.
        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ApiKeyRepository>();
            var toDelete = await repo.GetAll().Where(k => k.Name.StartsWith(tag)).ToListAsync(ct);
            toDelete.Should().HaveCount(2);
            repo.Remove(toDelete);
            await repo.SaveChanges();
        }

        using var verify = _factory.Services.CreateScope();
        var verifyRepo = verify.ServiceProvider.GetRequiredService<ApiKeyRepository>();
        (await verifyRepo.GetAll().AnyAsync(k => k.Id == id1 || k.Id == id2, ct))
            .Should()
            .BeFalse("every entity passed to Remove(IEnumerable) must be deleted");
    }
}
