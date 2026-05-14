using AwesomeAssertions;
using McpManager.Core.Mcp;
using Xunit;

namespace McpManager.UnitTests.Core.Mcp;

public class McpCommandBuilderTests
{
    [Fact]
    public void BuildCommandPreview_BlankCommand_ReturnsEmpty()
    {
        var result = McpCommandBuilder.BuildCommandPreview("   ", ["a", "b"]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildCommandPreview_ArgumentWithSpace_IsQuoted()
    {
        var result = McpCommandBuilder.BuildCommandPreview("npx", ["-y", "@scope/pkg name"]);

        result.Should().Be("npx -y \"@scope/pkg name\"");
    }

    [Fact]
    public void BuildCommandPreview_NullArguments_ReturnsCommandOnly()
    {
        var result = McpCommandBuilder.BuildCommandPreview("node", null);

        result.Should().Be("node");
    }

    [Fact]
    public void BuildCommandPreview_EmptyArgumentEntries_AreSkipped()
    {
        var result = McpCommandBuilder.BuildCommandPreview("node", ["", "server.js", ""]);

        result.Should().Be("node server.js");
    }

    [Fact]
    public void BuildNpxCommand_NoExtraArguments_ReturnsYFlagAndPackage()
    {
        var (command, arguments) = McpCommandBuilder.BuildNpxCommand("@scope/pkg", null);

        command.Should().Be("npx");
        arguments.Should().Equal("-y", "@scope/pkg");
    }

    [Fact]
    public void BuildNpxCommand_WithExtraArguments_SplitsByNewlineAndTrims()
    {
        var (command, arguments) = McpCommandBuilder.BuildNpxCommand(
            "@scope/pkg",
            "  --flag\n--other  \n\n--last"
        );

        command.Should().Be("npx");
        arguments.Should().Equal("-y", "@scope/pkg", "--flag", "--other", "--last");
    }

    [Fact]
    public void BuildCustomCommand_BlankArgumentsText_ReturnsEmptyArguments()
    {
        var (command, arguments) = McpCommandBuilder.BuildCustomCommand("python", "");

        command.Should().Be("python");
        arguments.Should().BeEmpty();
    }

    [Fact]
    public void BuildCustomCommand_NewlineSeparatedArguments_AreParsed()
    {
        var (command, arguments) = McpCommandBuilder.BuildCustomCommand(
            "python",
            "-m\nmymodule\n--port=8080"
        );

        command.Should().Be("python");
        arguments.Should().Equal("-m", "mymodule", "--port=8080");
    }
}
