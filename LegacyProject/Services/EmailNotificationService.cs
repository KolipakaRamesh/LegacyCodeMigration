using LegacyProject.Interfaces;

namespace LegacyProject.Services;

/// <summary>
/// Concrete implementation of <see cref="IEmailService"/> that simulates
/// sending transactional emails by writing to the console.
/// Replace with a real SMTP or third-party mail service in production.
/// </summary>
public class EmailNotificationService : IEmailService
{
    /// <inheritdoc/>
    public async Task SendOrderConfirmationAsync(string email, string orderNumber)
    {
        await Task.Delay(1); // simulate async I/O
        Console.WriteLine($"  [Email] ✉  Order confirmation → {email} | Order: {orderNumber}");
    }

    /// <inheritdoc/>
    public async Task SendInvoiceAsync(string email, string invoiceNumber, decimal amount)
    {
        await Task.Delay(1);
        Console.WriteLine($"  [Email] ✉  Invoice {invoiceNumber} (${amount:F2}) → {email}");
    }

    /// <inheritdoc/>
    public async Task SendShipmentNotificationAsync(string email, string orderNumber, string trackingNumber)
    {
        await Task.Delay(1);
        Console.WriteLine($"  [Email] ✉  Shipment → {email} | Order: {orderNumber} | Tracking: {trackingNumber}");
    }

    /// <inheritdoc/>
    public async Task SendPaymentReceiptAsync(string email, string transactionId, decimal amount)
    {
        await Task.Delay(1);
        Console.WriteLine($"  [Email] ✉  Payment receipt (${amount:F2}) → {email} | TxID: {transactionId}");
    }
}
