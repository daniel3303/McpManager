using System.Text.RegularExpressions;
using AwesomeAssertions;
using McpManager.FunctionalTests.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace McpManager.FunctionalTests;

[Collection("e2e")]
public class McpNamespacesFlowTests
{
    private readonly E2eFixture _e2e;

    public McpNamespacesFlowTests(E2eFixture e2e) => _e2e = e2e;

    /// <summary>
    /// Drives McpNamespacesController end-to-end through the real Kestrel
    /// pipeline in a real browser: login, Index render, then the Create POST
    /// flow (MapDtoToNamespace + McpNamespaceManager.Create + redirect) and the
    /// Show detail page. The whole namespaces dashboard was untested at the
    /// functional tier — only McpServers create/edit had browser coverage.
    /// </summary>
    [Fact]
    public async Task LoginCreateAndViewNamespace_ExercisesControllerThroughRealPipeline()
    {
        var page = await _e2e.NewPageAsync();

        await page.GotoAsync("/auth/login");
        await page.FillAsync("[name='Email']", "admin@mcpmanager.local");
        await page.FillAsync("[name='Password']", "123456");
        await page.ClickAsync("#loginBtn");
        await page.WaitForURLAsync(u => !u.Contains("/auth/login"));

        var index = await page.GotoAsync("/mcpnamespaces");
        index!.Status.Should().Be(200);

        await page.GotoAsync("/mcpnamespaces/create");
        var name = $"E2E NS {Guid.NewGuid():N}";
        var slug = $"e2ens{Guid.NewGuid():N}"[..14];
        await page.FillAsync("[name='Name']", name);
        await page.FillAsync("[name='Slug']", slug);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Namespace" }).ClickAsync();

        // Create POST -> McpNamespaceManager.Create -> redirect to Show, which
        // renders the persisted Name and Slug from MapNamespaceToDto.
        await page.WaitForURLAsync(
            new Regex(@"/mcpnamespaces/show/[0-9a-fA-F-]{36}$"),
            new PageWaitForURLOptions { Timeout = 30_000 }
        );

        var content = await page.ContentAsync();
        content.Should().Contain(name);
        content.Should().Contain(slug);
    }
}
