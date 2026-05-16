using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Identity;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class UsersControllerEditPostModelStateInvalidTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UsersControllerEditPostModelStateInvalidTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostEdit_WithInvalidEmail_ReRendersFormWithoutPersisting()
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

        var originalEmail = $"editms-{Guid.NewGuid():N}@example.com";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var u = new User
            {
                Email = originalEmail,
                UserName = originalEmail,
                GivenName = "Original",
                IsActive = true,
                EmailConfirmed = true,
            };
            (await users.CreateAsync(u, "abcdef")).Succeeded.Should().BeTrue();
            userId = u.Id;
        }

        var getResp = await client.GetAsync($"/Users/Edit?id={userId}", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // User exists (skips the not-found guard); a malformed Email fails the
        // [EmailAddress] check so ModelState is invalid -> View(dto). That
        // re-render branch was zero-hit (existing Edit tests post valid data
        // or hit the email-conflict path). A regression promoting the invalid
        // DTO past the gate would corrupt the user's email.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = "Renamed",
                ["Email"] = "not-a-valid-email",
                ["IsActive"] = "true",
                ["EmailConfirmed"] = "true",
            }
        );

        var response = await client.PostAsync($"/Users/Edit?id={userId}", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "an invalid email must re-render, not redirect");

        using var verify = _factory.Services.CreateScope();
        var users2 = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
        var reloaded = await users2.FindByIdAsync(userId.ToString());
        reloaded!.Email.Should().Be(originalEmail, "the rejected edit must not have persisted");
    }
}
