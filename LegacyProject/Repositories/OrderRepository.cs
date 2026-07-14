using LegacyProject.Base;
using LegacyProject.Infrastructure;
using LegacyProject.Models;
using LegacyProject.Models.Enums;

namespace LegacyProject.Repositories;

/// <summary>
/// Concrete repository for <see cref="Order"/> entities.
/// Provides order-specific filtering on top of the generic base.
/// </summary>
public class OrderRepository : BaseRepository<Order>
{
    private readonly DatabaseContext _context;

    public OrderRepository(DatabaseContext context)
    {
        _context = context;
    }

    /// <summary>Returns all orders belonging to a specific customer.</summary>
    public Task<IEnumerable<Order>> GetOrdersByCustomerIdAsync(Guid customerId)
    {
        _context.EnsureConnected();
        IEnumerable<Order> orders = _store.Where(o => o.Customer.Id == customerId).ToList();
        return Task.FromResult(orders);
    }

    /// <summary>Returns all orders with a specific status.</summary>
    public Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status)
    {
        _context.EnsureConnected();
        IEnumerable<Order> orders = _store.Where(o => o.Status == status).ToList();
        return Task.FromResult(orders);
    }

    /// <summary>Returns all orders with a <see cref="OrderStatus.Pending"/> status.</summary>
    public async Task<IEnumerable<Order>> GetPendingOrdersAsync()
    {
        return await GetOrdersByStatusAsync(OrderStatus.Pending);
    }

    /// <summary>Returns orders placed within the specified date range.</summary>
    public Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime from, DateTime to)
    {
        IEnumerable<Order> orders = _store
            .Where(o => o.CreatedAt >= from && o.CreatedAt <= to)
            .ToList();
        return Task.FromResult(orders);
    }
}
