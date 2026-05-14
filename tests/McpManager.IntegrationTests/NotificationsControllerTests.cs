using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class NotificationsControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public NotificationsControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetUnreadCount_AsAuthenticatedAdmin_ReturnsCamelCaseCountJson()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;

        await client.PostAsync(
            "/Auth/Login",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Email"] = "admin@mcpmanager.local",
                    ["Password"] = "123456",
                }
            ),
            ct
        );

        var response = await client.GetAsync("/Notifications/UnreadCount", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadAsStringAsync(ct);
        // Anonymous DTO is `new { Count = ... }` (PascalCase in C#).
        // CamelCasePropertyNamesContractResolver in Program.cs converts it
        // to "count" on the wire — a regression that drops the resolver
        // would silently return "Count" and break every frontend script
        // reading these JSON payloads.
        body.Should().Contain("\"count\"");
        body.Should().NotContain("\"Count\"");
    }
}
