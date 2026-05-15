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

public class UsersControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UsersControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetIndex_AsAuthenticatedAdmin_RendersListContainingSeededAdmin()
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

        var response = await client.GetAsync("/Users", ct);
        response.EnsureSuccessStatusCode();

        // UsersController inherits BaseController's [Authorize] only — no
        // policy guard, unlike McpServers/ApiKeys/AdminSettings. The seeded
        // admin row must show up in the rendered list; a missing seed or a
        // broken UserRepository.GetAll() would render an empty grid that
        // still returns 200.
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("admin@mcpmanager.local");
    }

    [Fact]
    public async Task PostCreate_WithValidDto_PersistsUserAndRedirectsToShow()
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

        var email = $"created-{Guid.NewGuid():N}@example.com";
        var getResp = await client.GetAsync("/Users/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var createHtml = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(createHtml), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = "Ada",
                ["Surname"] = "Lovelace",
                ["Email"] = email,
                ["Password"] = "Passw0rd!",
                ["ConfirmPassword"] = "Passw0rd!",
                ["IsActive"] = "true",
                ["EmailConfirmed"] = "true",
            }
        );

        // Create POST happy path: ModelState valid + email free + UserManager
        // .CreateAsync succeeds -> UpdateUserClaims -> 302 to Show. The whole
        // ~45-line action was uncovered; asserting the persisted user pins that
        // CreateAsync actually ran (a regression short-circuiting on the
        // duplicate-email or password-policy branch would never persist it).
        var response = await client.PostAsync("/Users/Create", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var created = await users.FindByEmailAsync(email);
        created.Should().NotBeNull("Create POST must persist the new user");
        created!.GivenName.Should().Be("Ada");
    }
}
