using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    [McpServerTool, Description("Echoes the supplied message back to the caller.")]
    public string Echo(string message) => $"echo: {message}";
}
