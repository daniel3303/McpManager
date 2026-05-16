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

public class UsersControllerEditEmailConflictTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UsersControllerEditEmailConflictTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostEdit_ChangingEmailToAnotherUsersEmail_ReRendersWithConflictError()
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

        var emailA = $"a-{Guid.NewGuid():N}@example.com";
        var emailB = $"b-{Guid.NewGuid():N}@example.com";
        Guid userAId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var a = new User
            {
                Email = emailA,
                UserName = emailA,
                IsActive = true,
                EmailConfirmed = true,
            };
            (await users.CreateAsync(a, "abcdef")).Succeeded.Should().BeTrue();
            userAId = a.Id;
            var b = new User
            {
                Email = emailB,
                UserName = emailB,
                IsActive = true,
                EmailConfirmed = true,
            };
            (await users.CreateAsync(b, "abcdef")).Succeeded.Should().BeTrue();
        }

        var getResp = await client.GetAsync($"/Users/Edit?id={userAId}", ct);
        getResp.EnsureSuccessStatusCode();
        var editHtml = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(editHtml), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // Editing user A to take user B's email hits the email-change branch
        // (A.Email != B.Email) -> FindByEmail returns B -> the conflict guard
        // (existingUser.Id != user.Id) which was zero-hit. A regression
        // dropping it would let two users share an email (Identity unique-email
        // violation / 500) instead of this re-rendered ModelState error.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = "Conflict",
                ["Surname"] = "Test",
                ["Email"] = emailB,
                ["IsActive"] = "true",
                ["EmailConfirmed"] = "true",
            }
        );

        var response = await client.PostAsync($"/Users/Edit?id={userAId}", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "the conflict must re-render, not redirect");
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("A user with this email already exists");

        using var verify = _factory.Services.CreateScope();
        var users2 = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
        (await users2.FindByIdAsync(userAId.ToString()))!
            .Email!.Should()
            .Be(emailA, "the conflicting edit must not change user A's email");
    }
}
