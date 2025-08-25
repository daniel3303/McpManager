using McpManager.Core.Data.Models.Mcp;

namespace McpManager.Core.Mcp;

public static class McpCommandBuilder {
    /// <summary>
    /// Builds command preview from an McpServer entity.
    /// </summary>
    public static string BuildCommandPreview(McpServer server) {
        return BuildCommandPreview(server.Command, server.Arguments);
    }

    /// <summary>
    /// Builds a displayable command string from explicit command and arguments.
    /// Arguments containing spaces are quoted.
    /// </summary>
    public static string BuildCommandPreview(string command, List<string> arguments) {
        if (string.IsNullOrWhiteSpace(command)) return "";

        var parts = new List<string> { command };
        foreach (var arg in arguments ?? []) {
            if (string.IsNullOrEmpty(arg)) continue;
            parts.Add(arg.Contains(' ') ? $"\"{arg}\"" : arg);
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Builds an npx command from an NPM package name and optional extra arguments (newline-separated).
    /// </summary>
    public static (string Command, List<string> Arguments) BuildNpxCommand(string npmPackage, string extraArguments) {
        var args = new List<string> { "-y", npmPackage };
        if (!string.IsNullOrWhiteSpace(extraArguments)) {
            args.AddRange(ParseLines(extraArguments));
        }
        return ("npx", args);
    }

    /// <summary>
    /// Builds a custom command from explicit command and newline-separated arguments text.
    /// </summary>
    public static (string Command, List<string> Arguments) BuildCustomCommand(string command, string argumentsText) {
        var args = string.IsNullOrWhiteSpace(argumentsText)
            ? new List<string>()
            : ParseLines(argumentsText);
        return (command, args);
    }

    private static List<string> ParseLines(string text) {
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
