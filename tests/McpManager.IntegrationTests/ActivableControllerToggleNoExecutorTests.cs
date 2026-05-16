using System.Net;
using AwesomeAssertions;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class ActivableControllerToggleNoExecutorTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public ActivableControllerToggleNoExecutorTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task PostIndex_ActivableWithoutExecutor_TogglesIsActiveAndPersists()
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

        Guid keyId;
        using (var scope = _factory.Services.CreateScope())
        {
            var created = await scope
                .ServiceProvider.GetRequiredService<ApiKeyManager>()
                .Create(new ApiKey { Name = $"activable-{Guid.NewGuid():N}" });
            keyId = created.Id;
        }

        // No IActivableExecutor<ApiKey> is registered, so the controller takes
        // the executor==null else branch: it flips IsActive directly, saves,
        // and returns Ok(model.IsActive). That whole branch + persistence was
        // zero-hit. A regression skipping SaveChanges (or the toggle) would
        // make the activate/deactivate grid button silently no-op.
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["modelName"] = typeof(ApiKey).AssemblyQualifiedName!,
                ["key"] = keyId.ToString(),
            }
        );

        var response = await client.PostAsync("/api/Activable", form, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(ct))
            .Should()
            .Contain("false", "a previously-active key must report inactive after the toggle");

        using var verify = _factory.Services.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<ApiKeyRepository>();
        var reloaded = await repo.GetAll().SingleAsync(k => k.Id == keyId, ct);
        reloaded.IsActive.Should().BeFalse("the toggled state must be persisted");
    }
}
