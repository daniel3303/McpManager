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

public class UsersControllerCreatePostModelStateInvalidTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UsersControllerCreatePostModelStateInvalidTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostCreate_WithInvalidEmail_ReRendersFormWithoutCreatingUser()
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

        // A malformed Email fails the [EmailAddress] check so the Create POST
        // returns View(dto) before FindByEmailAsync/CreateAsync run. That
        // !ModelState.IsValid branch was zero-hit (existing Create tests post
        // valid data). A regression promoting the invalid DTO would persist a
        // user with a broken email/username.
        var marker = $"badcreate-{Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = marker,
                ["Email"] = "not-a-valid-email",
                ["Password"] = "abcdef",
                ["ConfirmPassword"] = "abcdef",
            }
        );

        var response = await client.PostAsync("/Users/Create", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "an invalid email must re-render, not redirect");

        using var verify = _factory.Services.CreateScope();
        var users = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
        users
            .Users.Any(u => u.GivenName == marker)
            .Should()
            .BeFalse("the rejected form must not have created a user");
    }
}
