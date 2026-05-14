namespace McpManager.Core.Mcp.OpenApi;

public class OperationMetadata
{
    public string Method { get; set; }
    public string Path { get; set; }
    public List<ParameterMetadata> Parameters { get; set; } = [];
    public string RequestBodyContentType { get; set; }
}
