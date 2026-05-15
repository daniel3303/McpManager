using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Xunit;

namespace McpManager.IntegrationTests.Mcp;

public class McpServerManagerToolCustomizationTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServerManagerToolCustomizationTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task UpdateToolCustomization_WithDescriptionOnly_TrimsAndClearsSchemaOverride()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var tools = scope.ServiceProvider.GetRequiredService<McpToolRepository>();

        var tool = await SeedToolAsync(scope.ServiceProvider, "{}", "old custom schema");

        // Pass a description with surrounding whitespace and no argument
        // overrides — UpdateToolCustomization must Trim() the description
        // and null out CustomInputSchema so the proxy serves the original
        // input schema. The else-branch (schema=null) is reachable only
        // when argumentOverrides is empty/null.
        await sut.UpdateToolCustomization(tool, "  trimmed description  ", argumentOverrides: []);

        var persisted = await tools.GetAll().FirstAsync(t => t.Id == tool.Id, ct);
        persisted.CustomDescription.Should().Be("trimmed description");
        persisted.CustomInputSchema.Should().BeNull("no argument overrides means schema is reset");
    }

    [Fact]
    public async Task UpdateToolCustomization_WithArgumentOverride_RewritesSchemaPropertyDescription()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var tools = scope.ServiceProvider.GetRequiredService<McpToolRepository>();

        // Input schema has one property 'message' with the original
        // description. The override must rewrite that single field while
        // leaving the rest of the schema intact.
        const string schema = """
            {"type":"object","properties":{"message":{"type":"string","description":"original"}}}
            """;
        var tool = await SeedToolAsync(scope.ServiceProvider, schema, customSchema: null);

        await sut.UpdateToolCustomization(
            tool,
            customDescription: null,
            argumentOverrides: [("message", "overridden description")]
        );

        var persisted = await tools.GetAll().FirstAsync(t => t.Id == tool.Id, ct);
        persisted.CustomInputSchema.Should().NotBeNullOrEmpty();
        var parsed = JObject.Parse(persisted.CustomInputSchema!);
        parsed["properties"]!["message"]!["description"]!
            .Value<string>()
            .Should()
            .Be("overridden description");
    }

    private static async Task<McpTool> SeedToolAsync(
        IServiceProvider sp,
        string inputSchema,
        string customSchema
    )
    {
        var serverManager = sp.GetRequiredService<McpServerManager>();
        var server = await serverManager.Create(
            new McpServer
            {
                Name = $"custo-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://upstream.invalid/mcp",
            }
        );
        var tools = sp.GetRequiredService<McpToolRepository>();
        var tool = new McpTool
        {
            Name = "echo",
            Description = "original desc",
            InputSchema = inputSchema,
            CustomInputSchema = customSchema,
            McpServerId = server.Id,
        };
        tools.Add(tool);
        await tools.SaveChanges();
        return tool;
    }
}
