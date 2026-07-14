using LegacyProject.Base;
using LegacyProject.Infrastructure;
using LegacyProject.Models;

namespace LegacyProject.Repositories;

/// <summary>
/// Concrete repository for <see cref="Product"/> entities.
/// Supports category-based and stock-level queries.
/// </summary>
public class ProductRepository : BaseRepository<Product>
{
    private readonly DatabaseContext _context;

    public ProductRepository(DatabaseContext context)
    {
        _context = context;
    }

    /// <summary>Returns all products within a specific category.</summary>
    public Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        _context.EnsureConnected();
        IEnumerable<Product> result = _store
            .Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase) && p.IsActive)
            .ToList();
        return Task.FromResult(result);
    }

    /// <summary>Returns all products that are currently in stock.</summary>
    public Task<IEnumerable<Product>> GetInStockProductsAsync()
    {
        _context.EnsureConnected();
        IEnumerable<Product> result = _store.Where(p => p.IsInStock).ToList();
        return Task.FromResult(result);
    }

    /// <summary>Finds a product by its SKU code.</summary>
    public Task<Product?> GetBySkuAsync(string sku)
    {
        _context.EnsureConnected();
        var product = _store.FirstOrDefault(p =>
            p.SKU.Equals(sku, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(product);
    }

    /// <summary>Returns products with stock below the given threshold.</summary>
    public Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold = 10)
    {
        IEnumerable<Product> result = _store
            .Where(p => p.StockQuantity <= threshold && p.IsActive && !p.IsDiscontinued)
            .ToList();
        return Task.FromResult(result);
    }
}
