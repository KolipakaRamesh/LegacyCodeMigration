using LegacyProject.Base;
using LegacyProject.Models.Enums;

namespace LegacyProject.Models;

/// <summary>
/// Represents a payment transaction against an invoice.
/// </summary>
public class Payment : BaseEntity
{
    public Invoice Invoice { get; set; } = null!;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Marks the payment as successful and records the transaction ID.</summary>
    public void MarkAsPaid(string transactionId)
    {
        Status = PaymentStatus.Paid;
        TransactionId = transactionId;
        PaidAt = DateTime.UtcNow;
        MarkAsUpdated();
    }

    /// <summary>Marks the payment as failed.</summary>
    public void MarkAsFailed()
    {
        Status = PaymentStatus.Failed;
        MarkAsUpdated();
    }

    /// <summary>Marks the payment as refunded.</summary>
    public void MarkAsRefunded()
    {
        Status = PaymentStatus.Refunded;
        MarkAsUpdated();
    }

    /// <summary>True when the payment has been successfully processed.</summary>
    public bool IsSuccessful => Status == PaymentStatus.Paid;
}
