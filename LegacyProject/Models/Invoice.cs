using LegacyProject.Base;
using LegacyProject.Models.Enums;

namespace LegacyProject.Models;

/// <summary>
/// Represents an invoice generated for a completed order.
/// </summary>
public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Order Order { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    /// <summary>True when the invoice is unpaid and past its due date.</summary>
    public bool IsOverdue =>
        PaymentStatus == PaymentStatus.Pending && DueDate < DateTime.UtcNow;

    /// <summary>
    /// Initializes the invoice from an existing order.
    /// Sets the amount from the order total and applies a 30-day payment window.
    /// </summary>
    public Invoice(Order order, Customer customer)
    {
        Order = order;
        Customer = customer;
        Amount = order.TotalAmount;
        InvoiceDate = DateTime.UtcNow;
        DueDate = DateTime.UtcNow.AddDays(30);
    }

    /// <summary>Required for serialization / ORM frameworks.</summary>
    public Invoice() { }

    /// <summary>Marks the invoice as fully paid.</summary>
    public void MarkAsPaid()
    {
        PaymentStatus = PaymentStatus.Paid;
        MarkAsUpdated();
    }

    /// <summary>Marks the invoice as partially paid.</summary>
    public void MarkAsPartiallyPaid()
    {
        PaymentStatus = PaymentStatus.PartiallyPaid;
        MarkAsUpdated();
    }
}
