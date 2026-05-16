using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServerManagerApplyToolCustomizationsSkipTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServerManagerApplyToolCustomizationsSkipTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task ApplyToolCustomizations_BlankOrUnknownEntries_LeaveExistingToolUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var toolRepo = sp.GetRequiredService<McpToolRepository>();

        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"atc-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );
        var tool = toolRepo.Add(
            new McpTool
            {
                Name = "keep",
                McpServerId = server.Id,
                CustomDescription = "original",
            }
        );
        await toolRepo.SaveChanges();

        // Whitespace CustomDescription hits the IsNullOrWhiteSpace `continue`;
        // an unknown tool name hits the `tool == null` `continue` — both were
        // zero-hit. Pins that neither entry mutates an existing tool, so a
        // regression dropping either guard can't blank out real descriptions.
        await serverManager.ApplyToolCustomizations(
            server,
            new List<(string, string, string)>
            {
                ("keep", "orig-desc", "   "),
                ("ghost-tool", "orig-desc", "should be ignored"),
            }
        );

        var reloaded = await toolRepo.GetByName(server, "keep").FirstAsync(ct);
        reloaded.CustomDescription.Should().Be("original");
    }
}
