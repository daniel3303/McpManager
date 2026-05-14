namespace McpManager.Core.Data.Exceptions;

public class ApplicationException : Exception
{
    public string Property { get; }
    public List<string> Errors { get; }

    public ApplicationException(string message)
        : base(message)
    {
        Errors = [message];
    }

    public ApplicationException(string message, string property)
        : base(message)
    {
        Property = property;
        Errors = [message];
    }

    public ApplicationException(List<string> errors)
        : base(string.Join("; ", errors))
    {
        Errors = errors;
    }

    public ApplicationException(string property, List<string> errors)
        : base(string.Join("; ", errors))
    {
        Property = property;
        Errors = errors;
    }
}
