using LegacyProject.Interfaces;
using LegacyProject.Models;
using LegacyProject.Repositories;

namespace LegacyProject.Services;

/// <summary>
/// Processes payments against invoices and handles refunds.
/// Coordinates <see cref="InvoiceRepository"/>, <see cref="IInvoiceService"/>,
/// and <see cref="IEmailService"/>.
/// </summary>
public class PaymentService
{
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IInvoiceService _invoiceService;
    private readonly IEmailService _emailService;

    public PaymentService(
        InvoiceRepository invoiceRepository,
        IInvoiceService invoiceService,
        IEmailService emailService)
    {
        _invoiceRepository = invoiceRepository;
        _invoiceService = invoiceService;
        _emailService = emailService;
    }

    /// <summary>
    /// Processes a payment for the given invoice and payment method.
    /// Returns the resulting <see cref="Payment"/> record.
    /// </summary>
    public async Task<Payment> ProcessPaymentAsync(Guid invoiceId, string paymentMethod)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId)
            ?? throw new InvalidOperationException($"Invoice '{invoiceId}' not found.");

        var payment = new Payment
        {
            Invoice = invoice,
            Amount = invoice.Amount,
            PaymentMethod = paymentMethod
        };

        var transactionId = GenerateTransactionId();
        payment.MarkAsPaid(transactionId);

        await _invoiceService.MarkInvoiceAsPaidAsync(invoiceId, transactionId);
        return payment;
    }

    /// <summary>Refunds a previously processed payment and notifies the customer.</summary>
    public async Task<Payment> RefundPaymentAsync(Payment payment)
    {
        payment.MarkAsRefunded();
        await _emailService.SendPaymentReceiptAsync(
            payment.Invoice.Customer.Email,
            $"REFUND-{payment.TransactionId}",
            payment.Amount);
        return payment;
    }

    /// <summary>Returns all unpaid invoices that can be processed.</summary>
    public async Task<IEnumerable<Invoice>> GetPendingInvoicesAsync()
    {
        return await _invoiceRepository.GetByPaymentStatusAsync(Models.Enums.PaymentStatus.Pending);
    }

    private static string GenerateTransactionId() =>
        $"TXN-{Guid.NewGuid():N}".ToUpper();
}
