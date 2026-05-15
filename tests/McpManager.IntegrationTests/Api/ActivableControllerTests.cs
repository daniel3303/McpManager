using System.Net;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Api;

public class ActivableControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ActivableControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostIndex_DeactivatingTheOnlyActiveUser_ReturnsBadRequest()
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

        // The seeded admin (Id = IntToGuid(1)) is the only user in the DB,
        // which means the "at least one active user" guard fires before
        // the "cannot deactivate yourself" guard. A regression that drops
        // either guard would let an operator lock the platform out.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["modelName"] = "McpManager.Core.Data.Models.Identity.User, McpManager.Core.Data",
                ["key"] = "00000001-0000-0000-0000-000000000000",
            }
        );

        var response = await client.PostAsync("/api/Activable/Index", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("at least one active user");
    }

    [Fact]
    public async Task PostIndex_TogglingActiveMcpServer_DeactivatesItAndReturnsOk()
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

        McpServer server;
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
            server = await manager.Create(
                new McpServer
                {
                    Name = $"act-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
        }
        server.IsActive.Should().BeTrue("new servers default to active");

        // The no-executor toggle path (lines 53-56, 84, 87-88) was uncovered —
        // only the User self-deactivation guard had a test. With no registered
        // IActivableExecutor the controller flips IsActive directly and persists;
        // a regression skipping SaveChanges would 200 but leave the server active.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["modelName"] = "McpManager.Core.Data.Models.Mcp.McpServer, McpManager.Core.Data",
                ["key"] = server.Id.ToString(),
            }
        );

        var response = await client.PostAsync("/api/Activable/Index", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain("false");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<McpServerRepository>();
        var reloaded = await repo.Get(server.Id);
        reloaded!.IsActive.Should().BeFalse("the toggle must persist the deactivation");
    }
}
