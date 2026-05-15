namespace McpManager.IntegrationTests.Mcp;

/// <summary>
/// Locates the compiled McpManager.TestStdioServer.dll built alongside the
/// integration test project. The csproj wires it up as a non-referenced
/// project so the build always produces it, but the binary lives in its
/// own bin folder rather than being copied into our output directory.
/// </summary>
internal static class TestStdioServerLocator
{
    public static string DllPath { get; } = ResolveDllPath();

    private static string ResolveDllPath()
    {
        // AppContext.BaseDirectory points at the integration test bin folder:
        // .../tests/McpManager.IntegrationTests/bin/<Config>/<TFM>/
        // Walk up to the tests folder, then sideways into the test server.
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
                "Could not find McpManager.TestStdioServer.dll. The project "
                    + "reference in McpManager.IntegrationTests.csproj should "
                    + "have built it. Expected path: "
                    + candidate
            );
        }

        return candidate;
    }
}
