namespace LegacyProject.Models.Enums;

/// <summary>Represents the payment state of an invoice or payment record.</summary>
public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3,
    PartiallyPaid = 4
}
