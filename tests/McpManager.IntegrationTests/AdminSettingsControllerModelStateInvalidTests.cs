using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Repositories;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class AdminSettingsControllerModelStateInvalidTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public AdminSettingsControllerModelStateInvalidTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostIndex_WithNonNumericTimeout_ReRendersWithoutSaving()
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

        var getResp = await client.GetAsync("/AdminSettings", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("form input[name='AntiForgery']")!.GetAttribute("value")!;

        int beforeTimeout;
        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<AppSettingsRepository>();
            beforeTimeout = (await repo.Get(1))!.McpConnectionTimeoutSeconds;
        }

        // A non-numeric McpConnectionTimeoutSeconds fails int model binding, so
        // ModelState is invalid and the action returns View(dto) before the
        // settings are persisted. That guard was zero-hit (the happy-path test
        // posts valid integers). A regression skipping it would write a
        // default/garbage timeout into the live settings row.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["McpConnectionTimeoutSeconds"] = "not-a-number",
                ["McpRetryAttempts"] = "5",
            }
        );

        var response = await client.PostAsync("/AdminSettings", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "an invalid settings form must re-render, not redirect");

        using var verify = _factory.Services.CreateScope();
        var verifyRepo = verify.ServiceProvider.GetRequiredService<AppSettingsRepository>();
        (await verifyRepo.Get(1))!
            .McpConnectionTimeoutSeconds.Should()
            .Be(beforeTimeout, "the rejected form must not have persisted any change");
    }
}
