using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Repositories;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class AdminSettingsControllerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public AdminSettingsControllerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task GetIndex_AsSeededAdmin_RendersFormWithSeededAppSettings()
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

        var response = await client.GetAsync("/AdminSettings", ct);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);

        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);

        // ApplicationDbContext seeds AppSettings { Id = 1, McpConnectionTimeoutSeconds = 120,
        // McpRetryAttempts = 3 }. A migration that drops the seed row or renames the
        // columns would render empty inputs here even though the page still loads.
        document
            .QuerySelector("input[name='McpConnectionTimeoutSeconds']")!
            .GetAttribute("value")
            .Should()
            .Be("120");
        document
            .QuerySelector("input[name='McpRetryAttempts']")!
            .GetAttribute("value")
            .Should()
            .Be("3");
    }

    [Fact]
    public async Task PostIndex_WithValidSettings_PersistsValuesAndRedirects()
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
        var document = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = document
            .QuerySelector("form input[name='AntiForgery']")!
            .GetAttribute("value")!;
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["McpConnectionTimeoutSeconds"] = "200",
                ["McpRetryAttempts"] = "7",
            }
        );

        // Index POST happy path (lines 44-67) was uncovered — only GET was. A
        // regression short-circuiting before SaveChanges would 302 but discard
        // the operator's timeout/retry edits, silently keeping stale values.
        var response = await client.PostAsync("/AdminSettings", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<AppSettingsRepository>();
        var saved = await repo.Get(1);
        try
        {
            saved!.McpConnectionTimeoutSeconds.Should().Be(200);
            saved.McpRetryAttempts.Should().Be(7);
        }
        finally
        {
            // AppSettings is a shared singleton row (Id = 1) seeded to 120/3;
            // restore it so the sibling GET test stays isolated from this POST.
            saved!.McpConnectionTimeoutSeconds = 120;
            saved.McpRetryAttempts = 3;
            await repo.SaveChanges();
        }
    }
}
