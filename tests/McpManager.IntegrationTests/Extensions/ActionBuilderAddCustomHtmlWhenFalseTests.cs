using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Web.Portal.Extensions;
using Xunit;

namespace McpManager.IntegrationTests.Extensions;

public class ActionBuilderAddCustomHtmlWhenFalseTests
{
    [Fact]
    public void AddCustomHtml_WhenPredicateFalse_SkipsFactoryAndReturnsSelf()
    {
        var server = new McpServer { Name = "epsilon" };
        var sut = new ActionBuilder<McpServer>(server, null, null, "McpServers", null);

        // The `when` short-circuit of AddCustomHtml is the last zero-hit branch
        // of that method (verification-false and the happy path are pinned by
        // siblings). Asserts a row-level `when` returning false suppresses the
        // cell before invoking the factory — a regression dropping this gate
        // would emit HTML for rows that must hide the custom action.
        var result = sut.AddCustomHtml(
            _ => throw new InvalidOperationException("factory must not run"),
            when: _ => false
        );

        result.Should().BeSameAs(sut);
        sut.Html.Should().BeEmpty();
    }
}
