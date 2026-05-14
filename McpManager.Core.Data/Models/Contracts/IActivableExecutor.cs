namespace McpManager.Core.Data.Models.Contracts;

public interface IActivableExecutor<in TEntity>
    where TEntity : IActivable
{
    Task<bool> Activate(TEntity entity);
    Task<bool> Deactivate(TEntity entity);
}
