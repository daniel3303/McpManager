namespace McpManager.Web.Portal.Dtos;

public interface IFilterDto
{
    int GetActiveFilterCount()
    {
        var count = 0;
        foreach (var property in GetType().GetProperties())
        {
            var value = property.GetValue(this);
            if (value == null)
                continue;

            if (property.PropertyType == typeof(string))
            {
                if (!string.IsNullOrWhiteSpace((string)value))
                    count++;
                continue;
            }

            if (Nullable.GetUnderlyingType(property.PropertyType) != null)
            {
                count++;
                continue;
            }

            if (property.PropertyType.IsValueType)
            {
                var defaultValue = Activator.CreateInstance(property.PropertyType);
                if (!value.Equals(defaultValue))
                    count++;
            }
        }
        return count;
    }
}
