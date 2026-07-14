namespace LegacyProject.Interfaces;

/// <summary>
/// Service contract for sending transactional email notifications.
/// </summary>
public interface IEmailService
{
    Task SendOrderConfirmationAsync(string email, string orderNumber);
    Task SendInvoiceAsync(string email, string invoiceNumber, decimal amount);
    Task SendShipmentNotificationAsync(string email, string orderNumber, string trackingNumber);
    Task SendPaymentReceiptAsync(string email, string transactionId, decimal amount);
}
