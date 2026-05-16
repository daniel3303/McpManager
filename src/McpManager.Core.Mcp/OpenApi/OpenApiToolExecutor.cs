using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Equibles.Core.AutoWiring;
using McpManager.Core.Data.Models.Authentication;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace McpManager.Core.Mcp.OpenApi;

[Service]
public class OpenApiToolExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenApiToolExecutor(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ToolExecutionResult> Execute(
        McpServer server,
        McpTool tool,
        Dictionary<string, object> arguments
    )
    {
        var metadata = JsonConvert.DeserializeObject<OperationMetadata>(tool.Metadata);
        var httpClient = _httpClientFactory.CreateClient();

        ConfigureAuth(httpClient, server);
        ConfigureHeaders(httpClient, server);

        var path = ResolvePathParameters(metadata.Path, metadata.Parameters, arguments);
        var queryString = BuildQueryString(metadata.Parameters, arguments);
        var url = server.Uri.TrimEnd('/') + path + queryString;

        var request = new HttpRequestMessage(new HttpMethod(metadata.Method), url);

        // Build request body for methods that support it
        if (metadata.Method is "POST" or "PUT" or "PATCH")
        {
            var body = BuildRequestBody(metadata, arguments);
            if (body != null)
            {
                var contentType = metadata.RequestBodyContentType ?? "application/json";
                request.Content = new StringContent(body, Encoding.UTF8, contentType);
            }
        }

        var response = await httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        var result = new ToolExecutionResult
        {
            Success = response.IsSuccessStatusCode,
            Content = [new ToolContent { Type = "text", Text = responseBody }],
        };

        if (!response.IsSuccessStatusCode)
        {
            result.Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }

        return result;
    }

    /// <summary>
    /// Checks connectivity to the server base URL.
    /// </summary>
    public async Task<bool> CheckHealth(McpServer server)
    {
        var httpClient = _httpClientFactory.CreateClient();
        ConfigureAuth(httpClient, server);
        ConfigureHeaders(httpClient, server);

        var request = new HttpRequestMessage(HttpMethod.Head, server.Uri);
        var response = await httpClient.SendAsync(request);

        // Accept any non-5xx as healthy (some APIs return 404 for root)
        return (int)response.StatusCode < 500;
    }

    // Serialize a path/query argument in its OpenAPI/JSON canonical form.
    // .NET's default ToString() yields "True"/"False" for booleans and
    // culture-sensitive text for numbers; OpenAPI wire form is lowercase
    // booleans and invariant-culture numbers.
    private static string FormatParameterValue(object value)
    {
        return value switch
        {
            null => "",
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    private string ResolvePathParameters(
        string path,
        List<ParameterMetadata> parameters,
        Dictionary<string, object> arguments
    )
    {
        foreach (var param in parameters.Where(p => p.In == "path"))
        {
            if (arguments.TryGetValue(param.Name, out var value))
            {
                path = path.Replace(
                    $"{{{param.Name}}}",
                    Uri.EscapeDataString(FormatParameterValue(value))
                );
            }
        }
        return path;
    }

    private string BuildQueryString(
        List<ParameterMetadata> parameters,
        Dictionary<string, object> arguments
    )
    {
        var queryParams = new List<string>();
        foreach (var param in parameters.Where(p => p.In == "query"))
        {
            if (arguments.TryGetValue(param.Name, out var value) && value != null)
            {
                queryParams.Add(
                    $"{Uri.EscapeDataString(param.Name)}={Uri.EscapeDataString(FormatParameterValue(value))}"
                );
            }
        }
        return queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
    }

    private string BuildRequestBody(
        OperationMetadata metadata,
        Dictionary<string, object> arguments
    )
    {
        // If there's a "body" parameter, use it directly
        if (arguments.TryGetValue("body", out var bodyValue))
        {
            return bodyValue is string s ? s : JsonConvert.SerializeObject(bodyValue);
        }

        // Collect non-path, non-query arguments as body fields
        var pathAndQueryNames = new HashSet<string>(
            metadata
                .Parameters.Where(p => p.In is "path" or "query" or "header")
                .Select(p => p.Name)
        );

        var bodyFields = arguments
            .Where(a => !pathAndQueryNames.Contains(a.Key))
            .ToDictionary(a => a.Key, a => a.Value);

        return bodyFields.Any() ? JsonConvert.SerializeObject(bodyFields) : null;
    }

    private void ConfigureAuth(HttpClient httpClient, McpServer server)
    {
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );

        switch (server.Auth.Type)
        {
            case AuthType.Basic:
                var basicCredentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{server.Auth.Username}:{server.Auth.Password}")
                );
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    basicCredentials
                );
                break;

            case AuthType.Bearer:
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    server.Auth.Token
                );
                break;

            case AuthType.ApiKey:
                httpClient.DefaultRequestHeaders.Add(
                    server.Auth.ApiKeyName,
                    server.Auth.ApiKeyValue
                );
                break;
        }
    }

    private void ConfigureHeaders(HttpClient httpClient, McpServer server)
    {
        foreach (var header in server.CustomHeaders)
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}
