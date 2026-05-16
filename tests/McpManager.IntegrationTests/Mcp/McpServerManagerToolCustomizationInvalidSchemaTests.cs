using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpServerManagerToolCustomizationInvalidSchemaTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServerManagerToolCustomizationInvalidSchemaTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task UpdateToolCustomization_ArgumentOverrideWithUnparseableSchema_SwallowsAndKeepsDescription()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var sut = sp.GetRequiredService<McpServerManager>();
        var tools = sp.GetRequiredService<McpToolRepository>();

        var server = await sut.Create(
            new McpServer
            {
                Name = $"badschema-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );
        var tool = new McpTool
        {
            Name = "echo",
            Description = "orig",
            InputSchema = "not json {{",
            CustomInputSchema = "{\"kept\":true}",
            McpServerId = server.Id,
        };
        tools.Add(tool);
        await tools.SaveChanges();

        // An argument override enters the try, but the malformed InputSchema
        // makes JsonDocument.Parse throw — the catch must swallow it so the
        // description still persists and CustomInputSchema is left untouched.
        // That catch was zero-hit (every other test seeds valid JSON).
        await sut.UpdateToolCustomization(
            tool,
            customDescription: "new desc",
            argumentOverrides: [("anything", "x")]
        );

        var persisted = await tools.GetAll().FirstAsync(t => t.Id == tool.Id, ct);
        persisted.CustomDescription.Should().Be("new desc");
        persisted
            .CustomInputSchema.Should()
            .Be("{\"kept\":true}", "the catch must not overwrite the schema on parse failure");
    }
}
