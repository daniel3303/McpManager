using System.Net.Http;
using System.Text.Json.Nodes;
using Equibles.Core.AutoWiring;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace McpManager.Core.Mcp.OpenApi;

[Service]
public class OpenApiSpecParser
{
    // The YAML reader is not registered by default in Microsoft.OpenApi 3.x;
    // without an explicit OpenApiReaderSettings.AddYamlReader() call the
    // Parse(content, "yaml") overload throws NotSupportedException. Build a
    // single settings instance once and reuse it for every parse.
    private static readonly OpenApiReaderSettings ReaderSettings = CreateReaderSettingsWithYaml();

    private static OpenApiReaderSettings CreateReaderSettingsWithYaml()
    {
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();
        return settings;
    }

    public List<ParsedOperation> ParseSpec(string specContent)
    {
        var result = OpenApiModelFactory.Parse(specContent, "json", ReaderSettings);

        // If JSON parsing produced no paths, try YAML
        if (result.Document?.Paths == null || !result.Document.Paths.Any())
        {
            result = OpenApiModelFactory.Parse(specContent, "yaml", ReaderSettings);
        }

        if (result.Document?.Paths == null)
        {
            var errors = result.Diagnostic?.Errors?.Select(e => e.Message) ?? [];
            throw new InvalidOperationException(
                "Failed to parse OpenAPI specification: " + string.Join("; ", errors)
            );
        }

        var operations = new List<ParsedOperation>();

        foreach (var (path, pathItem) in result.Document.Paths)
        {
            foreach (var (method, operation) in pathItem.Operations)
            {
                var name = !string.IsNullOrWhiteSpace(operation.OperationId)
                    ? operation.OperationId
                    : BuildOperationName(method, path);

                var description =
                    operation.Summary
                    ?? operation.Description
                    ?? $"{method.Method.ToUpper()} {path}";

                var parameters = BuildParameterList(operation);
                var requestBodyContentType = GetRequestBodyContentType(operation);
                var inputSchema = BuildInputSchema(operation, description);

                var metadata = JsonConvert.SerializeObject(
                    new
                    {
                        Method = method.Method.ToUpper(),
                        Path = path,
                        Parameters = parameters,
                        RequestBodyContentType = requestBodyContentType,
                    }
                );

                operations.Add(
                    new ParsedOperation
                    {
                        Name = name,
                        Description = description,
                        InputSchema = inputSchema,
                        Metadata = metadata,
                    }
                );
            }
        }

        return operations;
    }

    private string BuildOperationName(HttpMethod method, string path)
    {
        // Convert /pets/{petId}/toys to pets_petId_toys
        var sanitized = path.Trim('/')
            .Replace("{", "")
            .Replace("}", "")
            .Replace("/", "_")
            .Replace("-", "_");

        return $"{method.Method.ToLower()}_{sanitized}";
    }

    private List<ParameterMetadata> BuildParameterList(OpenApiOperation operation)
    {
        var parameters = new List<ParameterMetadata>();

        foreach (var param in operation.Parameters ?? [])
        {
            parameters.Add(
                new ParameterMetadata
                {
                    Name = param.Name,
                    In = param.In?.ToString().ToLower() ?? "query",
                    Required = param.Required,
                }
            );
        }

        return parameters;
    }

    private string GetRequestBodyContentType(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content == null || !operation.RequestBody.Content.Any())
        {
            return null;
        }

        // Prefer application/json
        if (operation.RequestBody.Content.ContainsKey("application/json"))
        {
            return "application/json";
        }

        return operation.RequestBody.Content.Keys.First();
    }

    private string BuildInputSchema(OpenApiOperation operation, string description)
    {
        var schema = new JObject { ["type"] = "object", ["description"] = description };

        var properties = new JObject();
        var required = new JArray();

        // Add parameters as properties
        foreach (var param in operation.Parameters ?? [])
        {
            var prop = ConvertSchemaToJObject(param.Schema);
            prop["description"] = param.Description ?? $"{param.In} parameter: {param.Name}";
            properties[param.Name] = prop;

            if (param.Required)
            {
                required.Add(param.Name);
            }
        }

        // Add request body properties
        if (operation.RequestBody?.Content != null)
        {
            var contentType = GetRequestBodyContentType(operation);
            if (
                contentType != null
                && operation.RequestBody.Content.TryGetValue(contentType, out var mediaType)
            )
            {
                if (mediaType.Schema?.Properties != null && mediaType.Schema.Properties.Any())
                {
                    // Inline body properties
                    foreach (var (propName, propSchema) in mediaType.Schema.Properties)
                    {
                        var prop = ConvertSchemaToJObject(propSchema);
                        properties[propName] = prop;
                    }

                    foreach (var req in mediaType.Schema.Required ?? new HashSet<string>())
                    {
                        required.Add(req);
                    }
                }
                else
                {
                    // Treat entire body as a "body" parameter
                    var bodyProp = ConvertSchemaToJObject(mediaType.Schema);
                    bodyProp["description"] = "Request body";
                    properties["body"] = bodyProp;

                    if (operation.RequestBody.Required)
                    {
                        required.Add("body");
                    }
                }
            }
        }

        if (properties.HasValues)
        {
            schema["properties"] = properties;
        }

        if (required.Any())
        {
            schema["required"] = required;
        }

        return schema.ToString(Formatting.None);
    }

    private JObject ConvertSchemaToJObject(IOpenApiSchema schema)
    {
        if (schema == null)
        {
            return new JObject { ["type"] = "string" };
        }

        var obj = new JObject();

        if (schema.Type.HasValue)
        {
            // JsonSchemaType is a flags enum — nullable types produce "null, string" etc.
            // Convert to a JSON array when multiple types are set.
            var flags = schema.Type.Value;
            var types = Enum.GetValues<JsonSchemaType>()
                .Where(f => f != 0 && flags.HasFlag(f))
                .Select(f => f.ToString().ToLower())
                .ToList();

            if (types.Count == 1)
            {
                obj["type"] = types[0];
            }
            else if (types.Count > 1)
            {
                obj["type"] = new JArray(types);
            }
        }

        if (!string.IsNullOrWhiteSpace(schema.Description))
        {
            obj["description"] = schema.Description;
        }

        if (!string.IsNullOrWhiteSpace(schema.Format))
        {
            obj["format"] = schema.Format;
        }

        if (schema.Enum?.Any() == true)
        {
            var enumValues = new JArray();
            foreach (var val in schema.Enum)
            {
                if (val is JsonValue jsonVal)
                {
                    enumValues.Add(jsonVal.ToString());
                }
            }
            if (enumValues.Any())
            {
                obj["enum"] = enumValues;
            }
        }

        // Handle array items
        if (schema.Type == JsonSchemaType.Array && schema.Items != null)
        {
            obj["items"] = ConvertSchemaToJObject(schema.Items);
        }

        // Handle object properties
        if (schema.Properties?.Any() == true)
        {
            obj["type"] = "object";
            var props = new JObject();
            foreach (var (name, propSchema) in schema.Properties)
            {
                props[name] = ConvertSchemaToJObject(propSchema);
            }
            obj["properties"] = props;
        }

        return obj;
    }
}
