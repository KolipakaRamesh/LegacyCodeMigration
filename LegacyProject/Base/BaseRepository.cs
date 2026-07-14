using LegacyProject.Interfaces;

namespace LegacyProject.Base;

/// <summary>
/// Generic in-memory base repository providing default CRUD behaviour.
/// Concrete repositories inherit this and may override any method.
/// </summary>
/// <typeparam name="T">Entity type that extends <see cref="BaseEntity"/>.</typeparam>
public abstract class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
    /// <summary>The in-memory backing store shared within the repository instance.</summary>
    protected readonly List<T> _store = new();

    /// <inheritdoc/>
    public virtual Task<T?> GetByIdAsync(Guid id)
    {
        var entity = _store.FirstOrDefault(e => e.Id == id && e.IsActive);
        return Task.FromResult(entity);
    }

    /// <inheritdoc/>
    public virtual Task<IEnumerable<T>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<T>>(_store.Where(e => e.IsActive).ToList());
    }

    /// <inheritdoc/>
    public virtual Task<T> AddAsync(T entity)
    {
        _store.Add(entity);
        return Task.FromResult(entity);
    }

    /// <inheritdoc/>
    public virtual Task<T> UpdateAsync(T entity)
    {
        var index = _store.FindIndex(e => e.Id == entity.Id);
        if (index >= 0)
        {
            entity.MarkAsUpdated();
            _store[index] = entity;
        }
        return Task.FromResult(entity);
    }

    /// <inheritdoc/>
    public virtual Task<bool> DeleteAsync(Guid id)
    {
        var entity = _store.FirstOrDefault(e => e.Id == id);
        if (entity == null) return Task.FromResult(false);
        entity.Deactivate();
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public virtual Task<bool> ExistsAsync(Guid id)
    {
        return Task.FromResult(_store.Any(e => e.Id == id && e.IsActive));
    }

    /// <summary>Returns the total number of active records in this store.</summary>
    public int Count() => _store.Count(e => e.IsActive);
}
