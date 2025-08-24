using System.Net.Http.Headers;
using System.Text;
using McpManager.Core.Data.Models.Authentication;
using McpManager.Core.Data.Models.Mcp;
using Equibles.Core.AutoWiring;
using ModelContextProtocol.Client;

namespace McpManager.Core.Mcp.Client;

[Service]
public class McpClientFactory {
    private readonly IHttpClientFactory _httpClientFactory;

    public McpClientFactory(IHttpClientFactory httpClientFactory) {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<McpClient> Create(McpServer server) {
        return server.TransportType switch {
            McpTransportType.Stdio => await CreateStdioClient(server),
            // SSE and HTTP both use Streamable HTTP transport (SSE was deprecated in MCP spec)
            _ => await CreateHttpClient(server)
        };
    }

    private async Task<McpClient> CreateHttpClient(McpServer server) {
        var httpClient = _httpClientFactory.CreateClient();
        ConfigureHttpClient(httpClient, server);

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions {
                Endpoint = new Uri(server.Uri)
            },
            httpClient,
            ownsHttpClient: false);

        return await McpClient.CreateAsync(transport);
    }

    private async Task<McpClient> CreateStdioClient(McpServer server) {
        var transport = new StdioClientTransport(new StdioClientTransportOptions {
            Name = server.Name,
            Command = server.Command,
            Arguments = server.Arguments?.ToList(),
            EnvironmentVariables = server.EnvironmentVariables?.Count > 0
                ? server.EnvironmentVariables
                : null
        });

        return await McpClient.CreateAsync(transport);
    }

    private void ConfigureHttpClient(HttpClient httpClient, McpServer server) {
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        switch (server.Auth.Type) {
            case AuthType.Basic:
                var basicCredentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{server.Auth.Username}:{server.Auth.Password}"));
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", basicCredentials);
                break;

            case AuthType.Bearer:
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", server.Auth.Token);
                break;

            case AuthType.ApiKey:
                httpClient.DefaultRequestHeaders.Add(server.Auth.ApiKeyName, server.Auth.ApiKeyValue);
                break;

            case AuthType.None:
            default:
                break;
        }

        foreach (var header in server.CustomHeaders) {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}
