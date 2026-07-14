using LegacyProject.Helpers;
using LegacyProject.Interfaces;
using LegacyProject.Models;
using LegacyProject.Models.Enums;
using LegacyProject.Repositories;

namespace LegacyProject.Services;

/// <summary>
/// Handles invoice generation, retrieval, overdue tracking, and payment marking.
/// Coordinates with <see cref="InvoiceRepository"/> and <see cref="IEmailService"/>.
/// </summary>
public class InvoiceService : IInvoiceService
{
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IEmailService _emailService;

    public InvoiceService(InvoiceRepository invoiceRepository, IEmailService emailService)
    {
        _invoiceRepository = invoiceRepository;
        _emailService = emailService;
    }

    /// <inheritdoc/>
    public async Task<Invoice> GenerateInvoiceAsync(Order order)
    {
        var invoice = new Invoice(order, order.Customer)
        {
            InvoiceNumber = GenerateInvoiceNumber()
        };

        var saved = await _invoiceRepository.AddAsync(invoice);
        await _emailService.SendInvoiceAsync(
            order.Customer.Email, invoice.InvoiceNumber, invoice.Amount);

        return saved;
    }

    /// <inheritdoc/>
    public async Task<Invoice?> GetInvoiceAsync(Guid invoiceId)
    {
        ValidationHelper.ValidateGuid(invoiceId, nameof(invoiceId));
        return await _invoiceRepository.GetByIdAsync(invoiceId);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Invoice>> GetOverdueInvoicesAsync()
    {
        return await _invoiceRepository.GetOverdueInvoicesAsync();
    }

    /// <inheritdoc/>
    public async Task MarkInvoiceAsPaidAsync(Guid invoiceId, string transactionId)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId)
            ?? throw new InvalidOperationException($"Invoice '{invoiceId}' not found.");

        invoice.MarkAsPaid();
        await _invoiceRepository.UpdateAsync(invoice);
        await _emailService.SendPaymentReceiptAsync(
            invoice.Customer.Email, transactionId, invoice.Amount);
    }

    private static string GenerateInvoiceNumber() =>
        $"INV-{DateHelper.CurrentTimestamp()}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
}
