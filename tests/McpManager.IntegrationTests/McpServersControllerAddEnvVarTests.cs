using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerAddEnvVarTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerAddEnvVarTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task PostAddEnvVar_AppendsEmptyRowAndReturnsEnvVarsPartial()
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
                ["EnvVars[0].Key"] = "EXISTING",
                ["EnvVars[0].Value"] = "1",
            }
        );

        // AddEnvVar (ModelState.Clear + EnvVars.Add + _EnvVarsForm partial) was
        // uncovered (only the sibling AddHeader had a test). It must append one
        // extra empty row and re-render the partial; a regression breaking the
        // partial or route would 500 or fail to add the new row.
        var response = await client.PostAsync("/McpServers/AddEnvVar", form, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var keyInputs = doc.QuerySelectorAll("input[name$='.Key']");
        keyInputs.Length.Should().BeGreaterThan(1, "AddEnvVar must append a new env-var row");
    }
}
