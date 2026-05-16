using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Web.Portal.Extensions;
using Xunit;

namespace McpManager.IntegrationTests.Extensions;

public class ActionBuilderAddCustomTests
{
    [Fact]
    public void AddCustom_WithoutAction_RendersClientSideButtonWithIconAndDataAttributes()
    {
        var server = new McpServer { Name = "beta", Id = Guid.NewGuid() };
        var sut = new ActionBuilder<McpServer>(server, null, null, "McpServers", null);

        // No view calls AddCustom, so its whole body is zero-hit. The no-action
        // branch is the only path that needs neither LinkGenerator nor
        // HttpContext: it builds a client-side <button type="button"> with the
        // icon and caller data-attributes. Pins that omitting `action` yields a
        // JS-only button (not a nav link) and that the dataAttributes factory is
        // invoked with the row model — a regression here breaks every grid
        // button that drives client-side behaviour instead of navigating.
        var result = sut.AddCustom(
            text: "Ping",
            icon: "bolt",
            cssClass: "extra",
            dataAttributes: m => new Dictionary<string, string> { ["server"] = m.Name }
        );

        result.Should().BeSameAs(sut);
        sut.Html.Should().StartWith("<button type=\"button\"");
        sut.Html.Should().Contain("data-tooltip=\"Ping\"");
        sut.Html.Should().Contain("data-server=\"beta\"");
        sut.Html.Should().Contain("grid-action-btn extra");
    }
}
