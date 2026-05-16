using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp;

public class McpCommandBuilderServerOverloadTests
{
    [Fact]
    public void BuildCommandPreview_FromServerEntity_ForwardsCommandAndArguments()
    {
        var server = new McpServer { Command = "npx", Arguments = ["-y", "@scope/pkg name"] };

        // The McpServer overload (delegates to the (command, args) overload)
        // is zero-hit — every existing test calls the explicit overload. Pins
        // that it forwards server.Command + server.Arguments verbatim; a
        // regression passing the wrong fields would corrupt the UI command
        // preview while the lower overload's own tests stay green.
        var result = McpCommandBuilder.BuildCommandPreview(server);

        result.Should().Be("npx -y \"@scope/pkg name\"");
    }
}
