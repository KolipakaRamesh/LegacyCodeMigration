using LegacyProject.Models;

namespace LegacyProject.Interfaces;

/// <summary>
/// Service contract for order management operations.
/// </summary>
public interface IOrderService
{
    Task<Order> CreateOrderAsync(Guid customerId, List<(Guid ProductId, int Quantity)> items);
    Task<Order?> GetOrderAsync(Guid orderId);
    Task<IEnumerable<Order>> GetOrdersByCustomerAsync(Guid customerId);
    Task<Order> ConfirmOrderAsync(Guid orderId);
    Task<Order> ShipOrderAsync(Guid orderId);
    Task<bool> CancelOrderAsync(Guid orderId);
}
