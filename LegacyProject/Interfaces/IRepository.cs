namespace LegacyProject.Interfaces;

/// <summary>
/// Generic repository contract providing standard CRUD operations.
/// All concrete repositories must implement this interface.
/// </summary>
/// <typeparam name="T">The domain entity type.</typeparam>
public interface IRepository<T>
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
}
