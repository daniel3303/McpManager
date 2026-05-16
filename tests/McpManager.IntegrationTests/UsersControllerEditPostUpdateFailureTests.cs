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

public class UsersControllerEditPostUpdateFailureTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UsersControllerEditPostUpdateFailureTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostEdit_EmailRejectedByIdentityUsernamePolicy_ReRendersAndDoesNotPersist()
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

        var originalEmail = $"upd-{Guid.NewGuid():N}@example.com";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var u = new User
            {
                Email = originalEmail,
                UserName = originalEmail,
                IsActive = true,
                EmailConfirmed = true,
            };
            (await users.CreateAsync(u, "abcdef")).Succeeded.Should().BeTrue();
            userId = u.Id;
        }

        var getResp = await client.GetAsync($"/Users/Edit?id={userId}", ct);
        getResp.EnsureSuccessStatusCode();
        var editHtml = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(editHtml), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // Changing the email to a unique apostrophe address clears the
        // conflict guard and sets UserName to it; `'` is outside Identity's
        // AllowedUserNameCharacters so UpdateAsync fails. That
        // `!updateResult.Succeeded` branch (surface errors -> View(dto)) was
        // zero-hit; ignoring the result would persist an invalid username.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = "Upd",
                ["Surname"] = "Fail",
                ["Email"] = $"o'{Guid.NewGuid():N}@example.com",
                ["IsActive"] = "true",
                ["EmailConfirmed"] = "true",
            }
        );

        var response = await client.PostAsync($"/Users/Edit?id={userId}", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "an UpdateAsync failure must re-render, not redirect");

        using var verify = _factory.Services.CreateScope();
        var users2 = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
        var reloaded = await users2.FindByIdAsync(userId.ToString());
        reloaded!.Email.Should().Be(originalEmail, "the rejected update must not persist");
    }
}
