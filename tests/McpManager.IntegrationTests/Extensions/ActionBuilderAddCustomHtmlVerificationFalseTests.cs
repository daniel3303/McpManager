using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Web.Portal.Extensions;
using Xunit;

namespace McpManager.IntegrationTests.Extensions;

public class ActionBuilderAddCustomHtmlVerificationFalseTests
{
    [Fact]
    public void AddCustomHtml_VerificationFalse_SkipsFactoryAndReturnsSelf()
    {
        var server = new McpServer { Name = "delta" };
        var sut = new ActionBuilder<McpServer>(server, null, null, "McpServers", null);

        // The `verification == false` short-circuit is still zero-hit; the
        // happy path and `when` gate are pinned by sibling tests. Asserts that
        // verification:false suppresses the cell *before* invoking the factory
        // — a regression dropping this guard would emit HTML that depends on
        // an unverified caller condition.
        var result = sut.AddCustomHtml(
            _ => throw new InvalidOperationException("factory must not run"),
            verification: false
        );

        result.Should().BeSameAs(sut);
        sut.Html.Should().BeEmpty();
    }
}
