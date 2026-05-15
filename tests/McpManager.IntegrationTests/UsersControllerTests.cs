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

    [Fact]
    public async Task PostEdit_WithExistingUserAndValidChange_PersistsAndRedirectsToShow()
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

        var email = $"edit-{Guid.NewGuid():N}@example.com";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                GivenName = "Before",
                Surname = "User",
                Email = email,
                UserName = email,
                IsActive = true,
                EmailConfirmed = true,
            };
            (await users.CreateAsync(user, "Passw0rd!")).Succeeded.Should().BeTrue();
            userId = user.Id;
        }

        var getResp = await client.GetAsync($"/Users/Edit?id={userId}", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = "After",
                ["Surname"] = "User",
                ["Email"] = email, // unchanged -> skips the email-conflict branch
                ["IsActive"] = "true",
                ["EmailConfirmed"] = "true",
            }
        );

        // Edit POST happy path: user found + ModelState valid + email unchanged
        // + no password -> UpdateAsync -> UpdateUserClaims -> 302 to Show. The
        // ~50-line action was uncovered; asserting the persisted rename pins
        // that UpdateAsync ran rather than short-circuiting on a guard.
        var response = await client.PostAsync($"/Users/Edit?id={userId}", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var users2 = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
        var reloaded = await users2.FindByEmailAsync(email);
        reloaded!.GivenName.Should().Be("After", "Edit POST must persist the new given name");
    }

    [Fact]
    public async Task GetShow_WithExistingUser_RendersUserDetailPage()
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

        var email = $"show-{Guid.NewGuid():N}@example.com";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                GivenName = "Grace",
                Surname = "Hopper",
                Email = email,
                UserName = email,
                IsActive = true,
                EmailConfirmed = true,
            };
            (await users.CreateAsync(user, "Passw0rd!")).Succeeded.Should().BeTrue();
            userId = user.Id;
        }

        // Show was entirely uncovered (lines 60-76): found-guard bypass +
        // ClaimStore.ClaimGroups() ViewData + View(user). Show has no
        // [HttpGet("{id}")] so id binds from the query string. A regression in
        // the claim-groups load or the view throws a 500 on the user-detail page.
        var response = await client.GetAsync($"/Users/Show?id={userId}", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain(email);
    }

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
                GivenName = "A",
                Surname = "A",
                Email = emailA,
                UserName = emailA,
                IsActive = true,
                EmailConfirmed = true,
            };
            (await users.CreateAsync(a, "Passw0rd!")).Succeeded.Should().BeTrue();
            userAId = a.Id;
            var b = new User
            {
                GivenName = "B",
                Surname = "B",
                Email = emailB,
                UserName = emailB,
                IsActive = true,
                EmailConfirmed = true,
            };
            (await users.CreateAsync(b, "Passw0rd!")).Succeeded.Should().BeTrue();
        }

        var getResp = await client.GetAsync($"/Users/Edit?id={userAId}", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = "A",
                ["Surname"] = "A",
                ["Email"] = emailB, // collides with user B
                ["IsActive"] = "true",
                ["EmailConfirmed"] = "true",
            }
        );

        // Edit POST email-conflict branch (lines 188-197) was uncovered: when
        // the email changes to one another user already owns, it must re-render
        // the form (200) with a ModelState error, NOT 302/persist. A regression
        // dropping the conflict check would let two users share an email.
        var response = await client.PostAsync($"/Users/Edit?id={userAId}", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("A user with this email already exists.");

        using var verify = _factory.Services.CreateScope();
        var users2 = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
        var reloaded = await users2.FindByIdAsync(userAId.ToString());
        reloaded!.Email.Should().Be(emailA, "the conflicting email change must not persist");
    }

    [Fact]
    public async Task PostEdit_WithNewPassword_ResetsPasswordAndRedirectsToShow()
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

        var email = $"pwd-{Guid.NewGuid():N}@example.com";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                GivenName = "Pw",
                Surname = "User",
                Email = email,
                UserName = email,
                IsActive = true,
                EmailConfirmed = true,
            };
            (await users.CreateAsync(user, "OldPassw0rd!")).Succeeded.Should().BeTrue();
            userId = user.Id;
        }

        var getResp = await client.GetAsync($"/Users/Edit?id={userId}", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = "Pw",
                ["Surname"] = "User",
                ["Email"] = email,
                ["Password"] = "NewPassw0rd!",
                ["ConfirmPassword"] = "NewPassw0rd!",
                ["IsActive"] = "true",
                ["EmailConfirmed"] = "true",
            }
        );

        // The dto.Password branch (lines 209-213: GeneratePasswordResetToken +
        // ResetPasswordAsync) was uncovered — the happy Edit test sends no
        // password. Asserting the new password authenticates pins the admin
        // password-reset (a regression skipping ResetPassword would 302 while
        // leaving the old password active).
        var response = await client.PostAsync($"/Users/Edit?id={userId}", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var users2 = verify.ServiceProvider.GetRequiredService<UserManager<User>>();
        var reloaded = await users2.FindByIdAsync(userId.ToString());
        (await users2.CheckPasswordAsync(reloaded!, "NewPassw0rd!"))
            .Should()
            .BeTrue("Edit must reset to the new password");
        (await users2.CheckPasswordAsync(reloaded!, "OldPassw0rd!"))
            .Should()
            .BeFalse("the old password must no longer authenticate");
    }

    [Fact]
    public async Task PostCreate_WithExistingEmail_ReRendersWithConflictError()
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

        var email = $"dupe-{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var existing = new User
            {
                GivenName = "Existing",
                Surname = "User",
                Email = email,
                UserName = email,
                IsActive = true,
                EmailConfirmed = true,
            };
            (await users.CreateAsync(existing, "Passw0rd!")).Succeeded.Should().BeTrue();
        }

        var getResp = await client.GetAsync("/Users/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = "Dupe",
                ["Surname"] = "User",
                ["Email"] = email, // collides with the seeded user
                ["Password"] = "Passw0rd!",
                ["ConfirmPassword"] = "Passw0rd!",
                ["IsActive"] = "true",
                ["EmailConfirmed"] = "true",
            }
        );

        // Create POST's duplicate-email guard (lines 102-106: FindByEmailAsync
        // != null -> ModelState error + re-render) was uncovered — the happy
        // test always used a fresh email. A regression dropping it would create
        // a second account on an existing email and 200/302 silently.
        var response = await client.PostAsync("/Users/Create", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("A user with this email already exists.");
    }
}
