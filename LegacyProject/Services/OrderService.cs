using LegacyProject.Helpers;
using LegacyProject.Interfaces;
using LegacyProject.Models;
using LegacyProject.Models.Enums;
using LegacyProject.Repositories;

namespace LegacyProject.Services;

/// <summary>
/// Orchestrates the order creation and fulfilment workflow.
/// Coordinates between customer, product, and order repositories,
/// and delegates email notifications to <see cref="IEmailService"/>.
/// </summary>
public class OrderService : IOrderService
{
    private readonly OrderRepository _orderRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ProductRepository _productRepository;
    private readonly ICustomerService _customerService;
    private readonly IEmailService _emailService;

    public OrderService(
        OrderRepository orderRepository,
        CustomerRepository customerRepository,
        ProductRepository productRepository,
        ICustomerService customerService,
        IEmailService emailService)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _customerService = customerService;
        _emailService = emailService;
    }

    /// <inheritdoc/>
    public async Task<Order> CreateOrderAsync(Guid customerId, List<(Guid ProductId, int Quantity)> items)
    {
        ValidationHelper.ValidateGuid(customerId, nameof(customerId));

        var customer = await _customerRepository.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException($"Customer '{customerId}' not found.");

        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(),
            Customer = customer,
            DeliveryAddress = customer.ShippingAddress
        };

        foreach (var (productId, quantity) in items)
        {
            ValidationHelper.ValidateRange(quantity, 1, 1000, nameof(quantity));

            var product = await _productRepository.GetByIdAsync(productId)
                ?? throw new InvalidOperationException($"Product '{productId}' not found.");

            if (!product.IsInStock)
                throw new InvalidOperationException($"Product '{product.SKU}' is out of stock.");

            var discount = PriceCalculator.CalculateDiscount(product.UnitPrice, customer.CustomerType);
            var lineItem = new OrderItem
            {
                Product = product,
                Quantity = quantity,
                UnitPrice = product.UnitPrice,
                Discount = discount
            };

            order.AddItem(lineItem);
            product.ReduceStock(quantity);
            await _productRepository.UpdateAsync(product);
        }

        var created = await _orderRepository.AddAsync(order);
        await _emailService.SendOrderConfirmationAsync(customer.Email, order.OrderNumber);

        return created;
    }

    /// <inheritdoc/>
    public async Task<Order?> GetOrderAsync(Guid orderId)
    {
        return await _orderRepository.GetByIdAsync(orderId);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Order>> GetOrdersByCustomerAsync(Guid customerId)
    {
        return await _orderRepository.GetOrdersByCustomerIdAsync(customerId);
    }

    /// <inheritdoc/>
    public async Task<Order> ConfirmOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' not found.");

        order.Confirm();
        return await _orderRepository.UpdateAsync(order);
    }

    /// <inheritdoc/>
    public async Task<Order> ShipOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' not found.");

        var trackingNumber = GenerateTrackingNumber();
        order.Ship();

        await _orderRepository.UpdateAsync(order);
        await _emailService.SendShipmentNotificationAsync(
            order.Customer.Email, order.OrderNumber, trackingNumber);

        return order;
    }

    /// <inheritdoc/>
    public async Task<bool> CancelOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null) return false;

        if (order.Status is OrderStatus.Shipped or OrderStatus.Delivered)
            throw new InvalidOperationException(
                $"Cannot cancel order '{orderId}' — it has already been {order.Status}.");

        order.Cancel();
        await _orderRepository.UpdateAsync(order);
        return true;
    }

    private static string GenerateOrderNumber() =>
        $"ORD-{DateHelper.CurrentTimestamp()}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

    private static string GenerateTrackingNumber() =>
        $"TRK-{Guid.NewGuid().ToString()[..12].ToUpper()}";
}
