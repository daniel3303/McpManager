using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpRequestsControllerServerToolFilterTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpRequestsControllerServerToolFilterTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetIndex_WithServerIdAndToolIdFilters_ReturnsOnlyMatchingRequests()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var keepKey = $"keep-{Guid.NewGuid():N}";
        var dropKey = $"drop-{Guid.NewGuid():N}";
        Guid serverId,
            toolId;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var mgr = sp.GetRequiredService<McpServerManager>();
            var s1 = await mgr.Create(
                new McpServer
                {
                    Name = $"req-s1-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
            var s2 = await mgr.Create(
                new McpServer
                {
                    Name = $"req-s2-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
            var toolRepo = sp.GetRequiredService<McpToolRepository>();
            var t1 = toolRepo.Add(new McpTool { Name = "keep", McpServerId = s1.Id });
            var t2 = toolRepo.Add(new McpTool { Name = "drop", McpServerId = s2.Id });
            await toolRepo.SaveChanges();
            serverId = s1.Id;
            toolId = t1.Id;

            var reqRepo = sp.GetRequiredService<McpToolRequestRepository>();
            reqRepo.Add(
                new McpToolRequest
                {
                    McpToolId = t1.Id,
                    ApiKeyName = keepKey,
                    Parameters = "{}",
                    Response = "{}",
                    Success = true,
                }
            );
            reqRepo.Add(
                new McpToolRequest
                {
                    McpToolId = t2.Id,
                    ApiKeyName = dropKey,
                    Parameters = "{}",
                    Response = "{}",
                    Success = true,
                }
            );
            await reqRepo.SaveChanges();
        }

        // The Index ServerId and ToolId filter branches (two query.Where calls)
        // were both zero-hit — prior Index tests use no/Success filter only. A
        // regression dropping either Where would leak other servers'/tools'
        // requests into the scoped Request Log view.
        var response = await client.GetAsync(
            $"/McpRequests?ServerId={serverId}&ToolId={toolId}",
            ct
        );
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain(keepKey);
        body.Should()
            .NotContain(dropKey, "the ServerId/ToolId filters must exclude other requests");
    }
}
