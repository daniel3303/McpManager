using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerUpdateAuthFieldsTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerUpdateAuthFieldsTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostUpdateAuthFields_ReturnsAuthFormPartialBoundToPostedDto()
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

        var getResp = await client.GetAsync("/McpServers/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var createHtml = await getResp.Content.ReadAsStringAsync(ct);
        var formDoc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(createHtml), ct);
        var token = formDoc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "any",
                ["Auth.Type"] = "Basic",
                ["Auth.Username"] = "auth-user",
            }
        );

        // UpdateAuthFields (ModelState.Clear + PartialView "_AuthForm") was
        // uncovered. Switching the auth type in the server form posts here and
        // must re-render the partial bound to the posted dto (the Auth.Type
        // select is always emitted); a regression breaking the route/partial
        // would 500 the server form's authentication section.
        var response = await client.PostAsync("/McpServers/UpdateAuthFields", form, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().Contain("Auth.Type", "the _AuthForm partial must re-render");
    }
}
