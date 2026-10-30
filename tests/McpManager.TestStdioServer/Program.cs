using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// Minimal stdio MCP server used by integration tests as a real upstream
// for McpServerManager.CheckHealth / SyncTools success-path coverage.
// Logging is routed to stderr only; stdout is reserved for MCP protocol
// frames (the StdioServerTransport assumes exclusive ownership of stdout).
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders().AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<EchoTools>();
await builder.Build().RunAsync();

[McpServerToolType]
public class EchoTools
{
    // Many integration tests sync this server and assume it exposes exactly
    // one tool (some even assert that via .Single()/.SingleAsync() on the
    // synced rows), so structured-content coverage is added as an optional
    // parameter on the existing "echo" tool rather than as a second tool —
    // adding a second tool broke three unrelated NameOverride/
    // DescriptionOverride/DisabledTool end-to-end tests with "Sequence
    // contains more than one element." Returning CallToolResult directly
    // (instead of a plain string) lets the test server set StructuredContent
    // and IsError explicitly, for pinning the proxies' forwarding of those
    // fields on the success path (the failure path is already covered by
    // McpProxyServerCallToolErrorTests / McpNamespaceProxyServerCallToolErrorTests).
    [
        McpServerTool,
        Description(
            "Echoes the supplied message back to the caller. When returnStructured is true, also returns it as structuredContent."
        )
    ]
    public CallToolResult Echo(string message, bool returnStructured = false) =>
        new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"echo: {message}" }],
            StructuredContent = returnStructured
                ? JsonSerializer.SerializeToElement(new { echoed = message })
                : null,
            IsError = false,
        };
}
