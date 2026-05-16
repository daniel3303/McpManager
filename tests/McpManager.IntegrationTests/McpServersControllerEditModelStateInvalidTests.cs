using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerEditModelStateInvalidTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerEditModelStateInvalidTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostEdit_ExistingServerWithBlankName_ReRendersFormWithoutPersisting()
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

        var name = $"edit-invalid-{Guid.NewGuid():N}";
        McpServer server;
        using (var scope = _factory.Services.CreateScope())
        {
            server = await scope
                .ServiceProvider.GetRequiredService<McpServerManager>()
                .Create(
                    new McpServer
                    {
                        Name = name,
                        TransportType = McpTransportType.Http,
                        Uri = "https://upstream.invalid/mcp",
                    }
                );
        }

        var getResp = await client.GetAsync($"/McpServers/Edit/{server.Id}", ct);
        getResp.EnsureSuccessStatusCode();
        var editHtml = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(editHtml), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // Server exists (skips the not-found guard), the blank-key header
        // exercises the CustomHeaders Where filter, then [Required] Name fails
        // ModelState -> View("Form", dto). That filter + invalid-edit branch
        // was uncovered; a regression promoting the invalid DTO past the gate
        // would persist an empty server name.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = "",
                ["TransportType"] = "Http",
                ["Uri"] = "https://upstream.invalid/mcp",
                ["CustomHeaders[0].Key"] = "",
                ["CustomHeaders[0].Value"] = "ignored-blank-key",
            }
        );

        var response = await client.PostAsync($"/McpServers/Edit/{server.Id}", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "invalid edit must re-render, not redirect");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<McpServerRepository>();
        var reloaded = await repo.Get(server.Id);
        reloaded!.Name.Should().Be(name, "an invalid edit must not persist the blank name");
    }
}
