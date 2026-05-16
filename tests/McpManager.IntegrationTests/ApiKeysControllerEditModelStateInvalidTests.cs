using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class ApiKeysControllerEditModelStateInvalidTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ApiKeysControllerEditModelStateInvalidTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostEdit_ExistingKeyWithBlankName_ReturnsEditFormPartialWithoutRenaming()
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

        var name = $"apikey-edit-{Guid.NewGuid():N}";
        ApiKey created;
        using (var scope = _factory.Services.CreateScope())
        {
            created = await scope
                .ServiceProvider.GetRequiredService<ApiKeyManager>()
                .Create(new ApiKey { Name = name });
        }

        var getResp = await client.GetAsync($"/ApiKeys/Edit/{created.Id}", ct);
        getResp.EnsureSuccessStatusCode();
        var editHtml = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(editHtml), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // Key exists (skips the NotFound guard), then [Required] Name fails
        // ModelState -> PartialView("_EditForm", dto). The success path returns
        // JSON; this invalid branch was zero-hit. A regression promoting the
        // invalid DTO would rename the key to an empty string.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["AntiForgery"] = token, ["Name"] = "" }
        );

        var response = await client.PostAsync($"/ApiKeys/Edit/{created.Id}", form, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("Name is required");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<ApiKeyRepository>();
        var reloaded = await repo.Get(created.Id);
        reloaded!.Name.Should().Be(name, "an invalid rename must not persist");
    }
}
