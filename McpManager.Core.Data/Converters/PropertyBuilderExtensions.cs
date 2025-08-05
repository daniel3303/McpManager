using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McpManager.Core.Data.Converters;

public static class PropertyBuilderExtensions {
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> builder) where T : new() {
        builder.HasConversion(new JsonValueConverter<T>());
        builder.Metadata.SetValueComparer(new JsonValueComparer<T>());
        return builder;
    }
}
