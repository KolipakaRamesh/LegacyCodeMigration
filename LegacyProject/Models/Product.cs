using LegacyProject.Base;

namespace LegacyProject.Models;

/// <summary>
/// Represents a product available for purchase.
/// </summary>
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsDiscontinued { get; set; }

    /// <summary>True if the product has available stock and has not been discontinued.</summary>
    public bool IsInStock => StockQuantity > 0 && !IsDiscontinued && IsActive;

    /// <summary>Decreases available stock by the requested quantity.</summary>
    /// <exception cref="InvalidOperationException">Thrown when stock is insufficient.</exception>
    public void ReduceStock(int quantity)
    {
        if (quantity > StockQuantity)
            throw new InvalidOperationException(
                $"Insufficient stock for product '{SKU}'. Available: {StockQuantity}, Requested: {quantity}.");
        StockQuantity -= quantity;
    }

    /// <summary>Increases stock by the given quantity (e.g., restocking delivery).</summary>
    public void RestockProduct(int quantity) => StockQuantity += quantity;

    /// <summary>Discontinues the product so it can no longer be ordered.</summary>
    public void Discontinue()
    {
        IsDiscontinued = true;
        MarkAsUpdated();
    }
}
