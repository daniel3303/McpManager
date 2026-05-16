using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Web.Portal.Extensions;
using Xunit;

namespace McpManager.IntegrationTests.Extensions;

public class ActionBuilderAddCustomWhenFalseTests
{
    [Fact]
    public void AddCustom_WhenPredicateFalse_SkipsRenderingEntirelyAndReturnsSelf()
    {
        var server = new McpServer { Name = "gamma", Id = Guid.NewGuid() };
        var sut = new ActionBuilder<McpServer>(server, null, null, "McpServers", null);

        // The `when` short-circuit (return before any LinkGenerator/HttpContext
        // use) is the only AddCustom branch still zero-hit. Pins that a
        // row-level `when` returning false suppresses the button completely —
        // a regression dropping this gate would leak actions onto rows that
        // must not expose them (e.g. delete on a protected entity).
        var result = sut.AddCustom(
            text: "Danger",
            icon: "trash",
            action: "Delete",
            when: _ => false
        );

        result.Should().BeSameAs(sut);
        sut.Html.Should().BeEmpty();
    }
}
