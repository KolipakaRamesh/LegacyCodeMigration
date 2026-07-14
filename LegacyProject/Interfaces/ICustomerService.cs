using LegacyProject.Models;

namespace LegacyProject.Interfaces;

/// <summary>
/// Service contract for customer management operations.
/// </summary>
public interface ICustomerService
{
    Task<Customer> RegisterCustomerAsync(Customer customer);
    Task<Customer?> GetCustomerAsync(Guid customerId);
    Task<IEnumerable<Customer>> GetAllCustomersAsync();
    Task<Customer> UpdateCustomerAsync(Customer customer);
    Task<bool> DeactivateCustomerAsync(Guid customerId);
}
