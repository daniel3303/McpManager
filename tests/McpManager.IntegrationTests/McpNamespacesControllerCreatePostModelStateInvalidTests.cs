using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpNamespacesControllerCreatePostModelStateInvalidTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerCreatePostModelStateInvalidTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostCreate_BlankNameAndSlug_ReRendersFormWithoutPersisting()
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

        var getResp = await client.GetAsync("/McpNamespaces/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("form input[name='AntiForgery']")!.GetAttribute("value")!;

        // Every Create test posts a valid DTO, so the `!ModelState.IsValid ->
        // View("Form", dto)` branch was zero-hit. Blank [Required] Name+Slug
        // must re-render the form (HTTP 200), NOT 302 to Show — a regression
        // skipping the gate would persist an invalid namespace.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "",
                ["Slug"] = "",
                ["RateLimitRequestsPerMinute"] = "60",
            }
        );

        var response = await client.PostAsync("/McpNamespaces/Create", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
