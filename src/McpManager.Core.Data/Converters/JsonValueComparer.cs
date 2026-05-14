using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;

namespace McpManager.Core.Data.Converters;

public class JsonValueComparer<T> : ValueComparer<T>
    where T : new()
{
    public JsonValueComparer()
        : base(
            (a, b) => JsonConvert.SerializeObject(a) == JsonConvert.SerializeObject(b),
            v => JsonConvert.SerializeObject(v).GetHashCode(),
            v => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(v))!
        ) { }
}
