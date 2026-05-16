using System.Net;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests.Api;

public class ActivableControllerIndexUnknownModelTypeTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ActivableControllerIndexUnknownModelTypeTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostIndex_WithUnresolvableModelName_ReturnsBadRequest()
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

        // `modelName` is fed straight into Type.GetType (reflection by name from
        // the request). A bogus name makes it null -> the first validation gate
        // `BadRequest("Model type not found.")`, which no test reached: existing
        // cases all pass real types. A regression dropping that null guard would
        // NRE on the next reflection call (500) for any malformed model name.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["modelName"] = "Totally.Not.A.Real.Type, Nonexistent.Assembly",
                ["key"] = Guid.NewGuid().ToString(),
            }
        );

        var response = await client.PostAsync("/api/Activable/Index", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("Model type not found");
    }
}
