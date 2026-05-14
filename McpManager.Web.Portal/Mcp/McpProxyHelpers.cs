using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace McpManager.Web.Portal.Mcp;

public static class McpProxyHelpers
{
    private const string EmptyObjectSchema = """{"type":"object"}""";

    /// <summary>
    /// Parse a stored JSON schema string into a JsonElement, sanitizing it
    /// for JSON Schema draft 2020-12 compliance required by Claude's API.
    /// </summary>
    public static JsonElement ParseInputSchema(string inputSchema)
    {
        if (string.IsNullOrWhiteSpace(inputSchema))
        {
            using var doc = JsonDocument.Parse(EmptyObjectSchema);
            return doc.RootElement.Clone();
        }

        try
        {
            var jObj = JObject.Parse(inputSchema);
            SanitizeSchema(jObj);
            var json = jObj.ToString(Formatting.None);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            using var doc = JsonDocument.Parse(EmptyObjectSchema);
            return doc.RootElement.Clone();
        }
    }

    /// <summary>
    /// Recursively sanitize a JSON Schema object for 2020-12 compliance:
    /// - Remove $schema declarations (Claude infers the draft version)
    /// - Fix comma-separated type strings like "integer, string" → ["integer", "string"]
    /// - Ensure root has "type": "object"
    /// </summary>
    private static void SanitizeSchema(JObject schema, bool isRoot = true)
    {
        // Remove $schema — Claude rejects non-2020-12 declarations
        schema.Remove("$schema");

        // Ensure root has "type": "object"
        if (isRoot && schema["type"] == null)
        {
            schema["type"] = "object";
        }

        // Fix comma-separated type values → JSON array
        var typeToken = schema["type"];
        if (typeToken is JValue typeVal && typeVal.Type == JTokenType.String)
        {
            var typeStr = typeVal.Value<string>();
            if (typeStr != null && typeStr.Contains(','))
            {
                var types = typeStr
                    .Split(',')
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 0)
                    .ToArray();
                schema["type"] = new JArray(types);
            }
        }

        // Recurse into properties
        if (schema["properties"] is JObject props)
        {
            foreach (var prop in props.Properties())
            {
                if (prop.Value is JObject propObj)
                {
                    SanitizeSchema(propObj, isRoot: false);
                }
            }
        }

        // Recurse into items (array schemas)
        if (schema["items"] is JObject items)
        {
            SanitizeSchema(items, isRoot: false);
        }
    }

    public static Dictionary<string, object> ConvertArguments(
        IDictionary<string, JsonElement> arguments
    )
    {
        if (arguments == null)
        {
            return [];
        }

        var result = new Dictionary<string, object>();
        foreach (var kvp in arguments)
        {
            result[kvp.Key] = ConvertJsonElement(kvp.Value);
        }
        return result;
    }

    public static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element
                .EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.GetRawText(),
        };
    }
}
