using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.IntegrationTests.Mcp;

public class McpServerManagerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServerManagerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Create_WithEmptyName_ThrowsApplicationExceptionForName()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "",
                    TransportType = McpTransportType.Http,
                    Uri = "https://example.invalid/",
                }
            );

        // ValidateServer's first guard. A regression that moves the name check
        // after persistence would let blank-named servers reach the DB and
        // break the McpServers list view.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Name");
    }

    [Fact]
    public async Task Create_WithHttpTransportAndNonAbsoluteUri_ThrowsApplicationExceptionForUri()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "bad-uri",
                    TransportType = McpTransportType.Http,
                    Uri = "not-a-real-uri",
                }
            );

        // ValidateServer's Uri.TryCreate guard. The MCP HTTP client gets the
        // raw string into a new Uri() — a non-absolute value crashes the
        // factory later if this guard ever moves.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Uri");
    }

    [Fact]
    public async Task Create_WithValidHttpServer_PersistsRowAndAssignsId()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var ct = TestContext.Current.CancellationToken;
        var name = $"integration-{Guid.NewGuid():N}";

        var created = await sut.Create(
            new McpServer
            {
                Name = name,
                TransportType = McpTransportType.Http,
                Uri = "https://example.invalid/mcp",
            }
        );

        // Round-trip via the repository proves SaveChanges fired and the entity
        // is queryable by Id — without that contract Update and Delete would
        // also be broken.
        created.Id.Should().NotBeEmpty();
        var persisted = await repo.GetAll().FirstOrDefaultAsync(s => s.Id == created.Id, ct);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(name);
    }

    [Fact]
    public async Task Update_AfterCreate_ChangesAreVisibleViaRepository()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var ct = TestContext.Current.CancellationToken;

        var server = await sut.Create(
            new McpServer
            {
                Name = $"to-update-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://example.invalid/mcp",
            }
        );

        server.Description = "updated";
        await sut.Update(server);

        // Update calls SaveChanges on the tracked entity; if Validate ever
        // skipped on Update or SaveChanges wasn't awaited, the new
        // Description would not be observable via a fresh repository read.
        var persisted = await repo.GetAll().FirstAsync(s => s.Id == server.Id, ct);
        persisted.Description.Should().Be("updated");
    }

    [Fact]
    public async Task CheckHealth_WithUnreachableStdioCommand_ReturnsFalseAndStampsLastError()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();

        // /usr/bin/false exists on every Unix runner and exits non-zero
        // immediately. The stdio MCP client transport will fail to negotiate
        // and CheckHealth must catch, stamp LastError, and return false —
        // the only safe failure path for the operator UI.
        var server = await sut.Create(
            new McpServer
            {
                Name = $"unreachable-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "/usr/bin/false",
            }
        );

        var healthy = await sut.CheckHealth(server);

        healthy.Should().BeFalse();
        server.LastError.Should().NotBeNullOrWhiteSpace();
        server.LastError.Should().Contain("Failed to connect");
    }

    private McpServerManager ResolveServerManager()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<McpServerManager>();
    }
}
