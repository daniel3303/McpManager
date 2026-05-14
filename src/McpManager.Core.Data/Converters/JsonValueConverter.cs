using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

namespace McpManager.Core.Data.Converters;

public class JsonValueConverter<T> : ValueConverter<T, string>
    where T : new()
{
    public JsonValueConverter()
        : base(
            v => JsonConvert.SerializeObject(v),
            v => JsonConvert.DeserializeObject<T>(v) ?? new T()
        ) { }
}
