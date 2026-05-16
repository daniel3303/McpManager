using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServerManagerApplyToolCustomizationsNullTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServerManagerApplyToolCustomizationsNullTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task ApplyToolCustomizations_NullList_ReturnsWithoutTouchingTools()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var toolRepo = sp.GetRequiredService<McpToolRepository>();

        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"atc-null-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );
        toolRepo.Add(
            new McpTool
            {
                Name = "keep",
                McpServerId = server.Id,
                CustomDescription = "keepme",
            }
        );
        await toolRepo.SaveChanges();

        // The `customizations == null` short-circuit is still zero-hit (every
        // caller passes a list). Pins that a null list is a safe no-op — a
        // regression dropping the guard would NRE on the foreach instead.
        await serverManager.ApplyToolCustomizations(server, null);

        var reloaded = await toolRepo.GetByName(server, "keep").FirstAsync(ct);
        reloaded.CustomDescription.Should().Be("keepme");
    }
}
