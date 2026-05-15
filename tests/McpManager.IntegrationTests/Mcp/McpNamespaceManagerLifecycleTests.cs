using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpNamespaceManagerLifecycleTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceManagerLifecycleTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Update_RenamesNamespaceAndPersists()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
        var repo = scope.ServiceProvider.GetRequiredService<McpNamespaceRepository>();

        var ns = await sut.Create(
            new McpNamespace { Name = "Original", Slug = $"u{Guid.NewGuid():N}"[..16] }
        );

        ns.Name = "Renamed";
        await sut.Update(ns);

        // Update re-runs Validate + ValidateSlugUnique, then SaveChanges on
        // the tracked entity. A regression that bypassed SaveChanges would
        // leave the rename unobservable via a fresh repository read.
        var persisted = await repo.GetAll().FirstAsync(n => n.Id == ns.Id, ct);
        persisted.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task Delete_RemovesNamespaceRow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
        var repo = scope.ServiceProvider.GetRequiredService<McpNamespaceRepository>();

        var ns = await sut.Create(
            new McpNamespace { Name = "To Delete", Slug = $"d{Guid.NewGuid():N}"[..16] }
        );

        await sut.Delete(ns);

        var remaining = await repo.GetAll().AnyAsync(n => n.Id == ns.Id, ct);
        remaining.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleServer_TogglesIsActiveFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
        var serverManager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var nsServers = scope.ServiceProvider.GetRequiredService<McpNamespaceServerRepository>();

        var ns = await sut.Create(
            new McpNamespace { Name = "Toggle NS", Slug = $"t{Guid.NewGuid():N}"[..16] }
        );
        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"toggle-srv-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );
        var nsServer = await sut.AddServer(ns, server);

        // Toggle off, read back, toggle on, read back — the proxy filters
        // tools by McpNamespaceServer.IsActive, so this flag gates whether
        // tools are visible at /mcp/ns/{slug}.
        await sut.ToggleServer(nsServer, isActive: false);
        var afterOff = await nsServers.GetAll().FirstAsync(s => s.Id == nsServer.Id, ct);
        afterOff.IsActive.Should().BeFalse();

        await sut.ToggleServer(nsServer, isActive: true);
        var afterOn = await nsServers.GetAll().FirstAsync(s => s.Id == nsServer.Id, ct);
        afterOn.IsActive.Should().BeTrue();
    }
}
