using System.Text.RegularExpressions;
using AwesomeAssertions;
using McpManager.FunctionalTests.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace McpManager.FunctionalTests;

[Collection("e2e")]
public class ApiKeysFlowTests
{
    private readonly E2eFixture _e2e;

    public ApiKeysFlowTests(E2eFixture e2e) => _e2e = e2e;

    /// <summary>
    /// Drives ApiKeysController end-to-end through the real Kestrel pipeline in
    /// a real browser: login, Index render, then the Create POST flow
    /// (ApiKeyManager.Create generates the key + redirect) and the Show detail
    /// page. The ApiKeys dashboard had no functional-tier coverage at all.
    /// </summary>
    [Fact]
    public async Task LoginCreateAndViewApiKey_ExercisesControllerThroughRealPipeline()
    {
        var page = await _e2e.NewPageAsync();

        await page.GotoAsync("/auth/login");
        await page.FillAsync("[name='Email']", "admin@mcpmanager.local");
        await page.FillAsync("[name='Password']", "123456");
        await page.ClickAsync("#loginBtn");
        await page.WaitForURLAsync(u => !u.Contains("/auth/login"));

        var index = await page.GotoAsync("/apikeys");
        index!.Status.Should().Be(200);

        await page.GotoAsync("/apikeys/create");
        var name = $"E2E Key {Guid.NewGuid():N}";
        await page.FillAsync("[name='Name']", name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create API Key" }).ClickAsync();

        // Create POST -> ApiKeyManager.Create (generates the key) -> redirect to
        // Show, which renders the persisted Name and a masked key value.
        await page.WaitForURLAsync(
            new Regex(@"/apikeys/show/[0-9a-fA-F-]{36}$"),
            new PageWaitForURLOptions { Timeout = 30_000 }
        );

        (await page.ContentAsync()).Should().Contain(name);
    }
}
