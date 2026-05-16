using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpRequestsControllerIndexSuccessFilterTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpRequestsControllerIndexSuccessFilterTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task GetIndex_WithSuccessFilter_ExcludesFailedRequests()
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

        var okKey = $"ok-{Guid.NewGuid():N}";
        var failKey = $"fail-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var server = await sp.GetRequiredService<McpServerManager>()
                .Create(
                    new McpServer
                    {
                        Name = $"req-filter-{Guid.NewGuid():N}",
                        TransportType = McpTransportType.Http,
                        Uri = "https://upstream.invalid/mcp",
                    }
                );
            var toolRepo = sp.GetRequiredService<McpToolRepository>();
            var tool = toolRepo.Add(new McpTool { Name = "echo", McpServerId = server.Id });
            await toolRepo.SaveChanges();
            var reqRepo = sp.GetRequiredService<McpToolRequestRepository>();
            reqRepo.Add(
                new McpToolRequest
                {
                    McpToolId = tool.Id,
                    ApiKeyName = okKey,
                    Parameters = "{}",
                    Response = "{}",
                    Success = true,
                }
            );
            reqRepo.Add(
                new McpToolRequest
                {
                    McpToolId = tool.Id,
                    ApiKeyName = failKey,
                    Parameters = "{}",
                    Response = "{}",
                    Success = false,
                }
            );
            await reqRepo.SaveChanges();
        }

        // The Index Success-filter branch (query.Where(r => r.Success == ...))
        // was uncovered: the only prior Index test ran with no filter and an
        // empty DB. Asserting the failed request's ApiKeyName is absent while
        // the successful one's is present pins the predicate — a regression
        // dropping the Where would leak failed requests into the filtered view.
        var response = await client.GetAsync("/McpRequests?Success=true", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain(okKey);
        body.Should().NotContain(failKey, "the Success=true filter must exclude failed requests");
    }
}
