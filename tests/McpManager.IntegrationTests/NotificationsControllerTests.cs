using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Identity;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Notifications;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class NotificationsControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public NotificationsControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetUnreadCount_AsAuthenticatedAdmin_ReturnsCamelCaseCountJson()
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

        var response = await client.GetAsync("/Notifications/UnreadCount", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadAsStringAsync(ct);
        // Anonymous DTO is `new { Count = ... }` (PascalCase in C#).
        // CamelCasePropertyNamesContractResolver in Program.cs converts it
        // to "count" on the wire — a regression that drops the resolver
        // would silently return "Count" and break every frontend script
        // reading these JSON payloads.
        body.Should().Contain("\"count\"");
        body.Should().NotContain("\"Count\"");
    }

    [Fact]
    public async Task GetShow_WithUnreadNoUrlNotification_MarksReadAndRendersView()
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

        Guid notificationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var admin = await sp.GetRequiredService<UserRepository>()
                .GetAll()
                .FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);
            var created = await sp.GetRequiredService<NotificationManager>()
                .Create(admin, title: $"note-{Guid.NewGuid():N}", message: "hello");
            notificationId = created.Id;
        }

        // Only UnreadCount was covered. Show's found+unread+no-url path runs the
        // not-found bypass, the MarkAsRead branch, the empty-Url skip, and the
        // View render. Asserting the persisted IsRead pins the side effect — a
        // regression that drops MarkAsRead leaves the bell badge stuck.
        var response = await client.GetAsync($"/Notifications/Show/{notificationId}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<NotificationRepository>();
        var reloaded = await repo.Get(notificationId);
        reloaded!.IsRead.Should().BeTrue("Show must mark an unread notification as read");
    }

    [Fact]
    public async Task GetRecent_AsAuthenticatedAdmin_ReturnsRecentNotificationsJson()
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

        var title = $"recent-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var admin = await sp.GetRequiredService<UserRepository>()
                .GetAll()
                .FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);
            await sp.GetRequiredService<NotificationManager>()
                .Create(admin, title: title, message: "body");
        }

        // Recent (lines 138-157) was uncovered: GetByUser + OrderByDescending +
        // Take(5) + the anonymous projection -> Ok. It is the bell-dropdown's
        // data source; a regression in the projection or ordering surfaces as
        // an empty/garbled dropdown while the page still loads.
        var response = await client.GetAsync("/Notifications/Recent", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain(title);
    }

    [Fact]
    public async Task PostDelete_WithExistingNotification_RemovesItAndRedirectsToIndex()
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

        Guid notificationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var admin = await sp.GetRequiredService<UserRepository>()
                .GetAll()
                .FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);
            var created = await sp.GetRequiredService<NotificationManager>()
                .Create(admin, title: $"del-{Guid.NewGuid():N}", message: "x");
            notificationId = created.Id;
        }

        var indexResp = await client.GetAsync("/Notifications", ct);
        indexResp.EnsureSuccessStatusCode();
        var html = await indexResp.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        // Delete (lines 106-122) was uncovered: GetByUser scoping -> found-guard
        // bypass -> NotificationManager.Delete -> flash + redirect to Index.
        // Asserting the row is gone pins the per-user delete (a regression
        // dropping the GetByUser filter would also let users delete others').
        var response = await client.PostAsync($"/Notifications/Delete/{notificationId}", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<NotificationRepository>();
        var reloaded = await repo.Get(notificationId);
        reloaded.Should().BeNull("Delete must remove the notification row");
    }

    [Fact]
    public async Task PostMarkAllAsRead_WithUnreadNotifications_MarksThemAllReadAndRedirects()
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

        Guid id1;
        Guid id2;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var admin = await sp.GetRequiredService<UserRepository>()
                .GetAll()
                .FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);
            var manager = sp.GetRequiredService<NotificationManager>();
            id1 = (await manager.Create(admin, title: $"a-{Guid.NewGuid():N}")).Id;
            id2 = (await manager.Create(admin, title: $"b-{Guid.NewGuid():N}")).Id;
        }

        var indexResp = await client.GetAsync("/Notifications", ct);
        indexResp.EnsureSuccessStatusCode();
        var html = await indexResp.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        // MarkAllAsRead (lines 96-102) was uncovered: GetAuthenticatedUser ->
        // NotificationManager.MarkAllAsRead -> flash + redirect. Asserting both
        // seeded notifications flip to read pins the bulk mutation (a regression
        // scoping it wrong would leave the bell badge stuck or clear others').
        var response = await client.PostAsync("/Notifications/MarkAllAsRead", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<NotificationRepository>();
        (await repo.Get(id1))!.IsRead.Should().BeTrue();
        (await repo.Get(id2))!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task PostMarkAsRead_WithUnreadNotification_MarksItReadAndRedirects()
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

        Guid notificationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var admin = await sp.GetRequiredService<UserRepository>()
                .GetAll()
                .FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);
            var created = await sp.GetRequiredService<NotificationManager>()
                .Create(admin, title: $"mark-{Guid.NewGuid():N}", message: "x");
            notificationId = created.Id;
        }

        var indexResp = await client.GetAsync("/Notifications", ct);
        indexResp.EnsureSuccessStatusCode();
        var html = await indexResp.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        // MarkAsRead POST (lines 81-92) was uncovered: GetByUser scoping ->
        // found-guard -> NotificationManager.MarkAsRead -> redirect to Index.
        // Asserting the single row flips to read pins the per-user mutation (a
        // regression dropping GetByUser would let a user clear others' badges).
        var response = await client.PostAsync(
            $"/Notifications/MarkAsRead/{notificationId}",
            form,
            ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<NotificationRepository>();
        (await repo.Get(notificationId))!
            .IsRead.Should()
            .BeTrue("MarkAsRead must flip the targeted notification to read");
    }

    [Fact]
    public async Task PostApiMarkAsRead_WithUnreadNotification_ReturnsOkAndMarksRead()
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

        Guid notificationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var admin = await sp.GetRequiredService<UserRepository>()
                .GetAll()
                .FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);
            var created = await sp.GetRequiredService<NotificationManager>()
                .Create(admin, title: $"api-{Guid.NewGuid():N}", message: "x");
            notificationId = created.Id;
        }

        var indexResp = await client.GetAsync("/Notifications", ct);
        indexResp.EnsureSuccessStatusCode();
        var html = await indexResp.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        // ApiMarkAsRead's found path (lines 163-166,173-174) was uncovered — it
        // is the bell-dropdown's per-item AJAX action returning 200 Ok (not a
        // redirect). A regression in the GetByUser scoping or the side effect
        // leaves the badge stuck after the user clicks a single notification.
        var response = await client.PostAsync(
            $"/Notifications/ApiMarkAsRead?id={notificationId}",
            form,
            ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<NotificationRepository>();
        (await repo.Get(notificationId))!
            .IsRead.Should()
            .BeTrue("ApiMarkAsRead must flip the targeted notification to read");
    }

    [Fact]
    public async Task PostApiMarkAllAsRead_WithUnreadNotifications_ReturnsOkAndMarksAllRead()
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

        Guid id1,
            id2;
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var admin = await sp.GetRequiredService<UserRepository>()
                .GetAll()
                .FirstAsync(u => u.Email == "admin@mcpmanager.local", ct);
            var mgr = sp.GetRequiredService<NotificationManager>();
            id1 = (await mgr.Create(admin, title: $"all1-{Guid.NewGuid():N}", message: "x")).Id;
            id2 = (await mgr.Create(admin, title: $"all2-{Guid.NewGuid():N}", message: "y")).Id;
        }

        var indexResp = await client.GetAsync("/Notifications", ct);
        indexResp.EnsureSuccessStatusCode();
        var html = await indexResp.Content.ReadAsStringAsync(ct);
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token }
        );

        // ApiMarkAllAsRead (lines 181-184) was uncovered — it is the bell
        // dropdown's "mark all" AJAX action returning 200 Ok (not a redirect
        // like the page-level MarkAllAsRead). A regression scoping it wrong
        // leaves the badge stuck or clears another user's notifications.
        var response = await client.PostAsync("/Notifications/ApiMarkAllAsRead", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<NotificationRepository>();
        (await repo.Get(id1))!.IsRead.Should().BeTrue();
        (await repo.Get(id2))!.IsRead.Should().BeTrue();
    }
}
