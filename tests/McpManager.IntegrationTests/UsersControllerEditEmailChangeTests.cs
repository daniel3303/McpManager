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

public class UsersControllerEditEmailChangeTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UsersControllerEditEmailChangeTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostEdit_ChangingEmailToUnusedAddress_UpdatesBothEmailAndUserName()
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

        var oldEmail = $"old-{Guid.NewGuid():N}@example.com";
        var newEmail = $"new-{Guid.NewGuid():N}@example.com";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var u = new User
            {
                Email = oldEmail,
                UserName = oldEmail,
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

        // Email changes and no other user owns it, so the no-conflict branch
        // assigns BOTH user.UserName and user.Email = dto.Email. The happy-path
        // test keeps the email unchanged, so this was zero-hit. A regression
        // updating Email but not UserName would lock the user out (login uses
        // UserName).
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = "Renamed",
                ["Surname"] = "User",
                ["Email"] = newEmail,
                ["IsActive"] = "true",
                ["EmailConfirmed"] = "true",
            }
        );

        var response = await client.PostAsync($"/Users/Edit?id={userId}", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var users2 = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
        var reloaded = await users2.FindByIdAsync(userId.ToString());
        reloaded!.Email.Should().Be(newEmail);
        reloaded.UserName.Should().Be(newEmail, "UserName must track the new email for login");
    }
}
