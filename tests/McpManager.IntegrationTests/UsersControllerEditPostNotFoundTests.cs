using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class UsersControllerEditPostNotFoundTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public UsersControllerEditPostNotFoundTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostEdit_WithUnknownId_RedirectsToIndexWithError()
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

        var getResp = await client.GetAsync("/Users/Create", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // The POST Edit user == null guard (flash error + redirect to Index)
        // was zero-hit: the existing not-found test covers only GET Edit, and
        // every POST Edit test seeds the user. Submitting an edit for a user
        // deleted in another tab must redirect gracefully, not 500 on the
        // null user dereference further down.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["GivenName"] = "Ghost",
                ["Email"] = $"ghost-{Guid.NewGuid():N}@example.com",
            }
        );

        var response = await client.PostAsync($"/Users/Edit?id={Guid.NewGuid()}", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response
            .Headers.Location!.ToString()
            .Should()
            .Contain("/users", "an unknown user id must redirect back to the Index action");
    }
}
