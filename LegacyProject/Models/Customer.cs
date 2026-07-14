using LegacyProject.Base;
using LegacyProject.Models.Enums;

namespace LegacyProject.Models;

/// <summary>
/// Represents a registered customer in the order management system.
/// Inherits identity and audit fields from <see cref="BaseEntity"/>.
/// </summary>
public class Customer : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Address BillingAddress { get; set; } = new();
    public Address ShippingAddress { get; set; } = new();
    public CustomerType CustomerType { get; set; } = CustomerType.Standard;
    public decimal CreditLimit { get; set; }
    public List<Order> Orders { get; set; } = new();

    /// <summary>Returns the customer's full name.</summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>True if the customer has Premium or higher tier status.</summary>
    public bool IsPremium =>
        CustomerType == CustomerType.Premium ||
        CustomerType == CustomerType.VIP ||
        CustomerType == CustomerType.Corporate;

    /// <summary>Returns the total value of all active orders for this customer.</summary>
    public decimal GetTotalOrderValue() =>
        Orders.Sum(o => o.TotalAmount);
}
