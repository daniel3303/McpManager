using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class HomeControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public HomeControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Index_AnonymousRequest_RedirectsToLoginWithReturnUrl()
    {
        var client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            }
        );
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/Home/Index", ct);

        // OnRedirectToLogin in Program.cs uses LinkGenerator + Debug.Assert.
        // In Release builds the assert is stripped, so a null path would
        // redirect to "" — this test pins the resolved URL.
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        var location = response.Headers.Location!.ToString();
        // Routing has LowercaseUrls = true, so paths come back lowercased.
        location.Should().StartWithEquivalentOf("/auth/login");
        location.Should().Contain("ReturnUrl=");
        location.Should().ContainEquivalentOf("home");
    }

    [Fact]
    public async Task GetActiveApiKey_WithActiveKey_ReturnsSuccessJsonWithKey()
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

        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<ApiKeyManager>();
            await manager.Create(new ApiKey { Name = $"home-key-{Guid.NewGuid():N}" });
        }

        // ActiveApiKey's found path was uncovered (whole HomeController body is
        // 0%): it surfaces a real API key to the dashboard JS. A regression in
        // the IsActive filter or the JSON shape would break the dashboard's
        // copy-key widget while the page still loads.
        var response = await client.GetAsync("/Home/ActiveApiKey", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("key").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetIndex_AsAuthenticatedAdmin_RendersDashboardWithStatCounts()
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

        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
            await manager.Create(
                new McpServer
                {
                    Name = $"home-srv-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
        }

        // Index's four aggregate-count ViewData assignments (lines 33-39) were
        // uncovered. The view unboxes each via (int)ViewData[...]; dropping any
        // assignment (or breaking a Count predicate) would 500 the dashboard.
        var response = await client.GetAsync("/Home", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("Dashboard Overview");
    }
}
