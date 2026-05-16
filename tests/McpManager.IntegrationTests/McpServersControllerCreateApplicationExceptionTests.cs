using System.Net;
using AngleSharp;
using AwesomeAssertions;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class McpServersControllerCreateApplicationExceptionTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServersControllerCreateApplicationExceptionTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostCreate_HttpTransportWithBlankUri_ReRendersFormWithoutPersisting()
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
        var html = await getResp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);
        var token = doc.QuerySelector("input[name='AntiForgery']")!.GetAttribute("value")!;

        var name = $"create-appex-{Guid.NewGuid():N}";
        // Name + TransportType satisfy [Required] so ModelState is valid, but
        // HTTP transport with a blank Uri makes McpServerManager.Create throw
        // ApplicationException from ValidateServer. Only the catch
        // (ApplicationException) branch turns that into a ModelState error +
        // Form re-render; it was zero-hit (existing Create tests use valid
        // configs). A regression letting it escape would 500, not re-render.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["AntiForgery"] = token,
                ["Name"] = name,
                ["TransportType"] = "Http",
                ["Uri"] = "",
            }
        );

        var response = await client.PostAsync("/McpServers/Create", form, ct);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "an invalid server config must re-render, not redirect");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<McpServerRepository>();
        (await repo.GetAll().AnyAsync(s => s.Name == name, ct))
            .Should()
            .BeFalse("the rejected server must not have been persisted");
    }
}
