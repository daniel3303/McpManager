using System.Data;
using McpManager.Core.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace McpManager.Core.Repositories.Contracts;

/// <summary>
/// Provides a base repository implementation for entity data access, leveraging EF Core to interact with the database context.
/// This repository offers CRUD operations and customizable filtering for entities.
/// </summary>
/// <typeparam name="TEntity">The type of entity managed by this repository, constrained to reference types.</typeparam>
public abstract class BaseRepository<TEntity>
    where TEntity : class
{
    protected readonly ApplicationDbContext DbContext;

    protected BaseRepository(ApplicationDbContext dbContext)
    {
        DbContext = dbContext;
    }

    /// <summary>
    /// Returns an entity by its key (id).
    /// </summary>
    /// <param name="key"></param>
    /// <remarks>Getting by its key bypasses the company verification</remarks>
    /// <returns></returns>
    public virtual async Task<TEntity> Get(params object[] key)
    {
        return await DbContext.Set<TEntity>().FindAsync(key);
    }

    /// <summary>
    /// Returns all entities.
    /// </summary>
    /// <returns></returns>
    public virtual IQueryable<TEntity> GetAll()
    {
        return DbContext.Set<TEntity>().AsQueryable();
    }

    public virtual TEntity Add(TEntity entity)
    {
        DbContext.Set<TEntity>().Add(entity);
        return entity;
    }

    public virtual void Remove(TEntity entity)
    {
        DbContext.Set<TEntity>().Remove(entity);
    }

    /// <summary>
    /// Deletes a range of entities.
    /// The Removed is called for each entity, ensuring that all the validations are checked.
    /// </summary>
    public virtual void Remove(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            Remove(entity);
        }
    }

    public ApplicationDbContext GetDbContext()
    {
        return DbContext;
    }

    public virtual Task SaveChanges()
    {
        return DbContext.SaveChangesAsync();
    }

    public async Task<IDbContextTransaction> CreateTransaction(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = new CancellationToken()
    )
    {
        return await DbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    public IDbContextTransaction GetCurrentTransaction()
    {
        return DbContext.Database.CurrentTransaction;
    }

    public bool HasActiveTransaction()
    {
        return DbContext.Database.CurrentTransaction != null;
    }
}
