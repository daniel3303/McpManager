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

public class AccountControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public AccountControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetChangePassword_AsAuthenticatedAdmin_RendersFormWithCustomAntiforgeryField()
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

        var response = await client.GetAsync("/Account/ChangePassword", ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);

        // Program.cs configures AddAntiforgery(opts => opts.FormFieldName = "AntiForgery").
        // A regression to the default "__RequestVerificationToken" silently
        // breaks every [ValidateAntiForgeryToken] POST in the portal.
        var token = document.QuerySelector("form input[name='AntiForgery']");
        token.Should().NotBeNull("the custom antiforgery field name must be wired through");
        token!.GetAttribute("value").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostChangePassword_WithValidNewPassword_ResetsPasswordAndRedirects()
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
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;

        const string newPassword = "NewStr0ng!1";
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["NewPassword"] = newPassword,
                ["ConfirmPassword"] = newPassword,
            }
        );

        // ChangePassword POST happy path (lines 39-65) was uncovered — only GET
        // was. It resets via a generated token then 302s to itself; a regression
        // short-circuiting before ResetPasswordAsync would 302 but leave the old
        // password working, a silent security-relevant failure.
        try
        {
            var response = await client.PostAsync("/Account/ChangePassword", form, ct);
            response.StatusCode.Should().Be(HttpStatusCode.Found);

            using var verify = _factory.Services.CreateScope();
            var users = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
            var admin = await users.FindByEmailAsync("admin@mcpmanager.local");
            (await users.CheckPasswordAsync(admin!, newPassword))
                .Should()
                .BeTrue("the new password must authenticate after the reset");
        }
        finally
        {
            // Admin is the seeded singleton other tests sign in as (123456);
            // restore it so sibling tests' SignInAsAdminAsync keeps working.
            using var restore = _factory.Services.CreateScope();
            var users = restore.ServiceProvider.GetRequiredService<UserManager<User>>();
            var admin = await users.FindByEmailAsync("admin@mcpmanager.local");
            var resetToken = await users.GeneratePasswordResetTokenAsync(admin!);
            await users.ResetPasswordAsync(admin!, resetToken, "123456");
        }
    }
}
