using System.Net;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Identity;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests.Api;

public class ActivableControllerIndexSelfDeactivationTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ActivableControllerIndexSelfDeactivationTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostIndex_AdminDeactivatingThemselves_ReturnsBadRequest()
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

        // Add a second active user so the "at least one active user" guard
        // passes — only then does control reach the self-deactivation guard.
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var email = $"second-{Guid.NewGuid():N}@example.com";
            (
                await userManager.CreateAsync(
                    new User
                    {
                        UserName = email,
                        Email = email,
                        IsActive = true,
                        EmailConfirmed = true,
                    },
                    "abcdef"
                )
            ).Succeeded.Should().BeTrue("second active user is the precondition");
        }

        // The seeded admin (Id IntToGuid(1)) is the signed-in caller. With >1
        // active user the last-active guard is skipped, so model.Id ==
        // GetAuthenticatedUserId() fires: "You cannot deactivate your own
        // user." This guard was zero-hit (the existing test always tripped the
        // last-active guard first); dropping it would let an admin lock
        // themselves out.
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
        body.Should().Contain("cannot deactivate your own user");
    }
}
