using System.Text.RegularExpressions;
using AwesomeAssertions;
using McpManager.FunctionalTests.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace McpManager.FunctionalTests;

[Collection("e2e")]
public class ApiKeysDeleteFlowTests
{
    private readonly E2eFixture _e2e;

    public ApiKeysDeleteFlowTests(E2eFixture e2e) => _e2e = e2e;

    /// <summary>
    /// Exercises the form-confirm modal pipeline end-to-end in a real browser:
    /// the .form-confirm submit is intercepted by the Vite bundle, the custom
    /// &lt;dialog&gt; opens, clicking .confirm resubmits the real Delete POST.
    /// None of the confirm-modal JS path had functional coverage.
    /// </summary>
    [Fact]
    public async Task DeleteApiKeyViaConfirmModal_RemovesKeyAndRedirectsToIndex()
    {
        var page = await _e2e.NewPageAsync();

        await page.GotoAsync("/auth/login");
        await page.FillAsync("[name='Email']", "admin@mcpmanager.local");
        await page.FillAsync("[name='Password']", "123456");
        await page.ClickAsync("#loginBtn");
        await page.WaitForURLAsync(u => !u.Contains("/auth/login"));

        await page.GotoAsync("/apikeys/create");
        var name = $"E2E Del Key {Guid.NewGuid():N}";
        await page.FillAsync("[name='Name']", name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create API Key" }).ClickAsync();
        await page.WaitForURLAsync(
            new Regex(@"/apikeys/show/[0-9a-fA-F-]{36}$"),
            new PageWaitForURLOptions { Timeout = 30_000 }
        );
        var showUrl = page.Url;

        // Submit the Delete form-confirm; the bundle intercepts it and opens
        // #confirm-form-modal instead of posting. Clicking .confirm re-submits
        // the real POST -> Delete -> RedirectToAction(Index).
        await page.Locator("form[data-message*='Delete this API key'] button[type='submit']")
            .ClickAsync();
        await page.Locator("#confirm-form-modal .confirm").ClickAsync();

        await page.WaitForURLAsync(
            new Regex(@"/apikeys/?$"),
            new PageWaitForURLOptions { Timeout = 30_000 }
        );

        // The key is gone: its Show route now redirects back to Index
        // (ApiKeysController.Show -> not found -> RedirectToAction(Index)).
        await page.GotoAsync(showUrl);
        page.Url.Should().MatchRegex(@"/apikeys/?$");
        page.Url.Should().NotContain("/show/");
    }
}
