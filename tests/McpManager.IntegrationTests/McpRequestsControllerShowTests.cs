using System.Net;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpRequestsControllerShowTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpRequestsControllerShowTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetShow_WithExistingRequestId_RendersRequestDetailWithApiKeyName()
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

        var apiKeyName = $"caller-{Guid.NewGuid():N}";
        Guid requestId;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var server = await sp.GetRequiredService<McpServerManager>()
                .Create(
                    new McpServer
                    {
                        Name = $"req-show-{Guid.NewGuid():N}",
                        TransportType = McpTransportType.Http,
                        Uri = "https://upstream.invalid/mcp",
                    }
                );
            var toolRepo = sp.GetRequiredService<McpToolRepository>();
            var tool = toolRepo.Add(new McpTool { Name = "echo", McpServerId = server.Id });
            await toolRepo.SaveChanges();
            var reqRepo = sp.GetRequiredService<McpToolRequestRepository>();
            var request = reqRepo.Add(
                new McpToolRequest
                {
                    McpToolId = tool.Id,
                    ApiKeyName = apiKeyName,
                    Parameters = "{}",
                    Response = "{}",
                    Success = true,
                    ExecutionTimeMs = 7,
                }
            );
            await reqRepo.SaveChanges();
            requestId = request.Id;
        }

        // Only Show's not-found redirect was reachable before (no seeded
        // requests anywhere). The found path — Get(id) returns a row ->
        // View(request) renders Show.cshtml binding @Model.ApiKeyName — was
        // entirely uncovered. Asserting the rendered ApiKeyName pins that the
        // row loaded AND the detail view bound it; a regression that kept the
        // null guard always-true would 302 to Index instead of this 200.
        var response = await client.GetAsync($"/McpRequests/Show/{requestId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain(apiKeyName);
    }
}
