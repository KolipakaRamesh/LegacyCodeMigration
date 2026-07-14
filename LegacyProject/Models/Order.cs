using LegacyProject.Base;
using LegacyProject.Models.Enums;

namespace LegacyProject.Models;

/// <summary>
/// Represents a customer order containing one or more line items.
/// </summary>
public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public Customer Customer { get; set; } = null!;
    public List<OrderItem> Items { get; set; } = new();
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public Address DeliveryAddress { get; set; } = new();
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string Notes { get; set; } = string.Empty;

    /// <summary>Sum of all line totals before tax.</summary>
    public decimal SubTotal => Items.Sum(i => i.LineTotal);

    /// <summary>Tax amount calculated at 18%.</summary>
    public decimal TaxAmount => SubTotal * 0.18m;

    /// <summary>Final amount including tax.</summary>
    public decimal TotalAmount => SubTotal + TaxAmount;

    /// <summary>Adds a line item to the order.</summary>
    public void AddItem(OrderItem item) => Items.Add(item);

    /// <summary>Removes a line item from the order by product SKU.</summary>
    public void RemoveItem(Guid itemId) => Items.RemoveAll(i => i.Id == itemId);

    /// <summary>Marks the order as shipped and records the ship timestamp.</summary>
    public void Ship()
    {
        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
        MarkAsUpdated();
    }

    /// <summary>Marks the order as delivered and records the delivery timestamp.</summary>
    public void Deliver()
    {
        Status = OrderStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        MarkAsUpdated();
    }

    /// <summary>Cancels the order.</summary>
    public void Cancel()
    {
        Status = OrderStatus.Cancelled;
        MarkAsUpdated();
    }

    /// <summary>Confirms the order (moves from Pending to Confirmed).</summary>
    public void Confirm()
    {
        Status = OrderStatus.Confirmed;
        MarkAsUpdated();
    }
}
