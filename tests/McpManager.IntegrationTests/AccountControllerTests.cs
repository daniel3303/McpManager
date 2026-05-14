using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
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
}
