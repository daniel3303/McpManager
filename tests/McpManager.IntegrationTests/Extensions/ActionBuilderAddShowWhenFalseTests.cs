using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Web.Portal.Extensions;
using Xunit;

namespace McpManager.IntegrationTests.Extensions;

public class ActionBuilderAddShowWhenFalseTests
{
    [Fact]
    public void AddShow_WhenPredicateFalse_SkipsRenderingEntirelyAndReturnsSelf()
    {
        var server = new McpServer { Name = "epsilon", Id = Guid.NewGuid() };
        var sut = new ActionBuilder<McpServer>(server, null, null, "McpServers", null);

        // AddShow is entirely zero-hit; only its `when` short-circuit (return
        // before any LinkGenerator/HttpContext use) is reachable without real
        // routing infra. Pins that a row-level `when` returning false hides the
        // View link completely — a regression dropping this gate would leak a
        // View action onto rows that must not expose it.
        var result = sut.AddShow(text: "View", when: _ => false);

        result.Should().BeSameAs(sut);
        sut.Html.Should().BeEmpty();
    }
}
