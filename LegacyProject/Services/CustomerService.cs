using LegacyProject.Helpers;
using LegacyProject.Interfaces;
using LegacyProject.Models;
using LegacyProject.Repositories;

namespace LegacyProject.Services;

/// <summary>
/// Implements customer lifecycle operations including registration and profile management.
/// Depends on <see cref="CustomerRepository"/> for persistence and
/// <see cref="IEmailService"/> for welcome notifications.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly CustomerRepository _customerRepository;
    private readonly IEmailService _emailService;

    public CustomerService(CustomerRepository customerRepository, IEmailService emailService)
    {
        _customerRepository = customerRepository;
        _emailService = emailService;
    }

    /// <inheritdoc/>
    public async Task<Customer> RegisterCustomerAsync(Customer customer)
    {
        ValidationHelper.ValidateEmail(customer.Email);
        ValidationHelper.ValidateNotEmpty(customer.FirstName, nameof(customer.FirstName));
        ValidationHelper.ValidateNotEmpty(customer.LastName, nameof(customer.LastName));

        var existing = await _customerRepository.GetByEmailAsync(customer.Email);
        if (existing != null)
            throw new InvalidOperationException(
                $"A customer with email '{customer.Email}' is already registered.");

        return await _customerRepository.AddAsync(customer);
    }

    /// <inheritdoc/>
    public async Task<Customer?> GetCustomerAsync(Guid customerId)
    {
        ValidationHelper.ValidateGuid(customerId, nameof(customerId));
        return await _customerRepository.GetByIdAsync(customerId);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Customer>> GetAllCustomersAsync()
    {
        return await _customerRepository.GetAllAsync();
    }

    /// <inheritdoc/>
    public async Task<Customer> UpdateCustomerAsync(Customer customer)
    {
        ValidationHelper.ValidateEmail(customer.Email);
        ValidationHelper.ValidateNotEmpty(customer.FirstName, nameof(customer.FirstName));
        return await _customerRepository.UpdateAsync(customer);
    }

    /// <inheritdoc/>
    public async Task<bool> DeactivateCustomerAsync(Guid customerId)
    {
        ValidationHelper.ValidateGuid(customerId, nameof(customerId));
        return await _customerRepository.DeleteAsync(customerId);
    }
}
