namespace McpManager.Core.Data.Models.Contracts;

public interface IActivable
{
    public Guid Id { get; }
    public bool IsActive { get; set; }
}
