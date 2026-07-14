using LegacyProject.Base;
using LegacyProject.Infrastructure;
using LegacyProject.Models;

namespace LegacyProject.Repositories;

/// <summary>
/// Concrete repository for <see cref="Customer"/> entities.
/// Extends <see cref="BaseRepository{T}"/> with customer-specific queries.
/// </summary>
public class CustomerRepository : BaseRepository<Customer>
{
    private readonly DatabaseContext _context;

    public CustomerRepository(DatabaseContext context)
    {
        _context = context;
    }

    /// <summary>Finds a customer by their email address.</summary>
    public Task<Customer?> GetByEmailAsync(string email)
    {
        _context.EnsureConnected();
        var customer = _store.FirstOrDefault(c =>
            c.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && c.IsActive);
        return Task.FromResult(customer);
    }

    /// <summary>Returns all customers with a Premium, Corporate, or VIP tier.</summary>
    public Task<IEnumerable<Customer>> GetPremiumCustomersAsync()
    {
        _context.EnsureConnected();
        IEnumerable<Customer> premiums = _store.Where(c => c.IsPremium && c.IsActive).ToList();
        return Task.FromResult(premiiums);
    }

    /// <summary>Returns customers filtered by customer type.</summary>
    public Task<IEnumerable<Customer>> GetByTypeAsync(Models.Enums.CustomerType type)
    {
        IEnumerable<Customer> result = _store.Where(c => c.CustomerType == type && c.IsActive).ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public override Task<IEnumerable<Customer>> GetAllAsync()
    {
        _context.EnsureConnected();
        return base.GetAllAsync();
    }
}
