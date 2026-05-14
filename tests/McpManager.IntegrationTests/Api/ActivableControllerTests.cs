using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
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
}
