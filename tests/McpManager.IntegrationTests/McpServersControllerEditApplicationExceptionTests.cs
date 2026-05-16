using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerEditApplicationExceptionTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerEditApplicationExceptionTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostEdit_HttpTransportWithBlankUri_ReRendersFormWithoutPersisting()
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

        var name = $"edit-appex-{Guid.NewGuid():N}";
        const string originalUri = "https://upstream.invalid/mcp";
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
                        Uri = originalUri,
                    }
                );
        }

        var getResp = await client.GetAsync($"/McpServers/Edit/{server.Id}", ct);
        getResp.EnsureSuccessStatusCode();
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        // Server exists (skips not-found) and Name+TransportType keep ModelState
        // valid, but blanking the Uri makes McpServerManager.Update throw
        // ApplicationException from ValidateServer. Only the catch
        // (ApplicationException) branch re-renders the Form; it was zero-hit
        // (existing Edit tests keep a valid config). A regression letting it
        // escape would 500, or worse persist the invalid server.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = name,
                ["TransportType"] = "Http",
                ["Uri"] = "",
            }
        );

        var response = await client.PostAsync($"/McpServers/Edit/{server.Id}", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "an invalid server config must re-render, not redirect");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<McpServerRepository>();
        var reloaded = await repo.GetAll().SingleAsync(s => s.Id == server.Id, ct);
        reloaded.Uri.Should().Be(originalUri, "the rejected edit must not have been persisted");
    }
}
