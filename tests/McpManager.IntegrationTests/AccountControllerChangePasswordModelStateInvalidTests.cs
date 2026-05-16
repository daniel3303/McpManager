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

public class AccountControllerChangePasswordModelStateInvalidTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public AccountControllerChangePasswordModelStateInvalidTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostChangePassword_WithMismatchedConfirmation_ReRendersWithoutChanging()
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

        var getResp = await client.GetAsync("/Account/ChangePassword", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("form input[name='AntiForgery']")!.GetAttribute("value")!;

        // [Compare] makes ConfirmPassword != NewPassword fail validation, so
        // the !ModelState.IsValid branch returns View(dto) *before* the reset
        // token is generated. That branch was zero-hit (the happy-path test
        // posts a matching pair). A regression skipping the guard would reset
        // the password to NewPassword despite the typo'd confirmation.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["NewPassword"] = "abcdef1",
                ["ConfirmPassword"] = "totally-different",
            }
        );

        var response = await client.PostAsync("/Account/ChangePassword", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "an invalid change-password form must re-render, not redirect");

        // The seeded admin password (123456) must still authenticate — the
        // guard returned before ResetPasswordAsync ran.
        using var verify = _factory.Services.CreateScope();
        var users = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
        var admin = await users.FindByEmailAsync("admin@mcpmanager.local");
        (await users.CheckPasswordAsync(admin!, "123456"))
            .Should()
            .BeTrue("the rejected form must not have changed the password");
    }
}
