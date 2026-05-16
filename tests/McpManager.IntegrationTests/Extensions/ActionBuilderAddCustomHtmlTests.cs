using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Web.Portal.Extensions;
using Xunit;

namespace McpManager.IntegrationTests.Extensions;

public class ActionBuilderAddCustomHtmlTests
{
    [Fact]
    public void AddCustomHtml_WithHtmlFactory_AppendsModelRenderedHtmlAndReturnsSelf()
    {
        // No Razor view calls AddCustomHtml/AddInfo/AddCustom, so this whole
        // member is zero-hit by the integration suite. Construct ActionBuilder
        // directly: AddCustomHtml's happy path never touches HttpContext /
        // LinkGenerator / the grid column, so nulls are safe here.
        var server = new McpServer { Name = "alpha" };
        var sut = new ActionBuilder<McpServer>(server, null, null, "McpServers", null);

        // Pins the contract grid custom-actions depend on: the factory is
        // invoked with the row model and its output is appended verbatim, and
        // the call is fluent. A regression dropping `Html += action(Model)` or
        // the `return this` would silently erase every custom grid button.
        var result = sut.AddCustomHtml(m => $"<i data-name=\"{m.Name}\">x</i>");

        sut.Html.Should().Be("<i data-name=\"alpha\">x</i>");
        result.Should().BeSameAs(sut);
    }
}
