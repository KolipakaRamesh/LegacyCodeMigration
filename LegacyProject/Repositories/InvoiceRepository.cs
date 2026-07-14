using LegacyProject.Base;
using LegacyProject.Infrastructure;
using LegacyProject.Models;
using LegacyProject.Models.Enums;

namespace LegacyProject.Repositories;

/// <summary>
/// Concrete repository for <see cref="Invoice"/> entities.
/// Provides overdue and payment-status filtering.
/// </summary>
public class InvoiceRepository : BaseRepository<Invoice>
{
    private readonly DatabaseContext _context;

    public InvoiceRepository(DatabaseContext context)
    {
        _context = context;
    }

    /// <summary>Returns all invoices that are past due and still unpaid.</summary>
    public Task<IEnumerable<Invoice>> GetOverdueInvoicesAsync()
    {
        _context.EnsureConnected();
        IEnumerable<Invoice> result = _store.Where(i => i.IsOverdue).ToList();
        return Task.FromResult(result);
    }

    /// <summary>Returns invoices filtered by payment status.</summary>
    public Task<IEnumerable<Invoice>> GetByPaymentStatusAsync(PaymentStatus status)
    {
        _context.EnsureConnected();
        IEnumerable<Invoice> result = _store.Where(i => i.PaymentStatus == status).ToList();
        return Task.FromResult(result);
    }

    /// <summary>Finds the invoice associated with a particular order.</summary>
    public Task<Invoice?> GetByOrderIdAsync(Guid orderId)
    {
        _context.EnsureConnected();
        var invoice = _store.FirstOrDefault(i => i.Order.Id == orderId);
        return Task.FromResult(invoice);
    }

    /// <summary>Returns all invoices issued to a given customer.</summary>
    public Task<IEnumerable<Invoice>> GetByCustomerIdAsync(Guid customerId)
    {
        IEnumerable<Invoice> result = _store
            .Where(i => i.Customer.Id == customerId)
            .ToList();
        return Task.FromResult(result);
    }
}
