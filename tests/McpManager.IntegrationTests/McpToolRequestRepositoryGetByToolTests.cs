using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpToolRequestRepositoryGetByToolTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpToolRequestRepositoryGetByToolTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetByTool_ReturnsOnlyRequestsForThatToolId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var toolRepo = sp.GetRequiredService<McpToolRepository>();
        var reqRepo = sp.GetRequiredService<McpToolRequestRepository>();

        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"req-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );
        var toolA = toolRepo.Add(new McpTool { Name = "a", McpServerId = server.Id });
        var toolB = toolRepo.Add(new McpTool { Name = "b", McpServerId = server.Id });
        await toolRepo.SaveChanges();
        reqRepo.Add(new McpToolRequest { McpToolId = toolA.Id, Success = true });
        await reqRepo.SaveChanges();

        // GetByTool is zero-hit (the requests UI lists via other queries). Pins
        // that it filters strictly by McpToolId — a regression broadening the
        // predicate would leak one tool's call history into another's view.
        var forA = await reqRepo.GetByTool(toolA).ToListAsync(ct);
        var forB = await reqRepo.GetByTool(toolB).ToListAsync(ct);

        forA.Should().ContainSingle().Which.McpToolId.Should().Be(toolA.Id);
        forB.Should().BeEmpty();
    }
}
