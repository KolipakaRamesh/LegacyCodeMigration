namespace LegacyProject.Models.Enums;

/// <summary>Represents the lifecycle status of an order.</summary>
public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    Refunded = 6
}
