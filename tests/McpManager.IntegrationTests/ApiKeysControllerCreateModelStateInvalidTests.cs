using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeysControllerCreateModelStateInvalidTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeysControllerCreateModelStateInvalidTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostCreate_WithBlankName_ReRendersFormWithoutCreatingKey()
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

        var getResp = await client.GetAsync("/ApiKeys/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // ApiKeyDto.Name is [Required]; a blank Name fails ModelState so the
        // action returns View("Form", dto) before _apiKeyManager.Create runs.
        // That guard was zero-hit (the happy-path test posts a Name). A
        // regression skipping it would persist a nameless key.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token, ["Name"] = "" }
        );

        var response = await client.PostAsync("/ApiKeys/Create", form, ct);

        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "an invalid create form must re-render, not redirect");
        (await response.Content.ReadAsStringAsync(ct))
            .Should()
            .Contain("name=\"Name\"", "the create form must be re-rendered");
    }
}
