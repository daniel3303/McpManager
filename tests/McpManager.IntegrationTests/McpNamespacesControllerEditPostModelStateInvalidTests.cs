using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpNamespacesControllerEditPostModelStateInvalidTests
    : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespacesControllerEditPostModelStateInvalidTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostEdit_ExistingNamespaceBlankName_ReRendersFormWithoutPersisting()
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

        var slug = "ns-" + Guid.NewGuid().ToString("n")[..8];
        McpNamespace ns;
        using (var scope = _factory.Services.CreateScope())
        {
            ns = await scope
                .ServiceProvider.GetRequiredService<McpNamespaceManager>()
                .Create(new McpNamespace { Name = "Original", Slug = slug });
        }

        var getResp = await client.GetAsync($"/McpNamespaces/Edit/{ns.Id}", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("form input[name='AntiForgery']")!.GetAttribute("value")!;

        // The namespace exists (clears the ns==null guard), but a blank
        // [Required] Name fails ModelState — the `!ModelState.IsValid ->
        // View("Form", dto)` Edit branch was zero-hit (every Edit test posts a
        // valid DTO). A regression skipping the gate would blank the name.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "",
                ["Slug"] = slug,
                ["RateLimitRequestsPerMinute"] = "60",
            }
        );

        var response = await client.PostAsync($"/McpNamespaces/Edit/{ns.Id}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
