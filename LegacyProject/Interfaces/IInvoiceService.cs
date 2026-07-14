using LegacyProject.Models;

namespace LegacyProject.Interfaces;

/// <summary>
/// Service contract for invoice generation and payment tracking.
/// </summary>
public interface IInvoiceService
{
    Task<Invoice> GenerateInvoiceAsync(Order order);
    Task<Invoice?> GetInvoiceAsync(Guid invoiceId);
    Task<IEnumerable<Invoice>> GetOverdueInvoicesAsync();
    Task MarkInvoiceAsPaidAsync(Guid invoiceId, string transactionId);
}
