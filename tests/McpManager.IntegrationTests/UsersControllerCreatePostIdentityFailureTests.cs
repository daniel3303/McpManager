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

public class UsersControllerCreatePostIdentityFailureTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UsersControllerCreatePostIdentityFailureTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostCreate_EmailRejectedByIdentityUsernamePolicy_ReRendersWithoutCreatingUser()
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

        var getResp = await client.GetAsync("/Users/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // An apostrophe passes [EmailAddress] and is unique (FindByEmailAsync
        // misses), so ModelState is valid and the guard is cleared — but `'`
        // is outside Identity's default AllowedUserNameCharacters, so
        // CreateAsync fails. That `!result.Succeeded` branch (surface Identity
        // errors -> View(dto)) was zero-hit; a regression ignoring the result
        // would persist a user the Identity store rejected.
        var marker = $"idfail-{Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = marker,
                ["Email"] = $"o'{Guid.NewGuid():N}@example.com",
                ["Password"] = "abcdef",
                ["ConfirmPassword"] = "abcdef",
            }
        );

        var response = await client.PostAsync("/Users/Create", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "a CreateAsync failure must re-render, not redirect");

        using var verify = _factory.Services.CreateScope();
        var users = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
        users
            .Users.Any(u => u.GivenName == marker)
            .Should()
            .BeFalse("an Identity-rejected create must not persist a user");
    }
}
