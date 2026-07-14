namespace LegacyProject.Models;

/// <summary>
/// Represents a single line item within an order.
/// </summary>
public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }

    /// <summary>Total price for this line after discount and quantity multiplication.</summary>
    public decimal LineTotal => (UnitPrice - Discount) * Quantity;

    /// <summary>Returns the effective unit price after discount.</summary>
    public decimal EffectiveUnitPrice => UnitPrice - Discount;
}
