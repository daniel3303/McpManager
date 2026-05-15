namespace McpManager.FunctionalTests.Support;

/// <summary>
/// Locates the compiled McpManager.TestStdioServer.dll built alongside this
/// project (wired as a non-referenced ProjectReference, binary only). Used to
/// register a real stdio MCP upstream through the UI so McpServersController's
/// live SyncTools success path is exercised end-to-end.
/// </summary>
internal static class TestStdioServerLocator
{
    public static string DllPath { get; } = Resolve();

    private static string Resolve()
    {
        // AppContext.BaseDirectory:
        //   .../tests/McpManager.FunctionalTests/bin/<Config>/<TFM>/
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var tfm = Path.GetFileName(baseDir);
        var config = Path.GetFileName(Path.GetDirectoryName(baseDir)!);

        var candidate = Path.GetFullPath(
            Path.Combine(
                baseDir,
                "..",
                "..",
                "..",
                "..",
                "McpManager.TestStdioServer",
                "bin",
                config,
                tfm,
                "McpManager.TestStdioServer.dll"
            )
        );

        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException(
                "Could not find McpManager.TestStdioServer.dll. The non-referenced "
                    + "ProjectReference in McpManager.FunctionalTests.csproj should have "
                    + "built it. Expected path: "
                    + candidate
            );
        }

        return candidate;
    }
}
