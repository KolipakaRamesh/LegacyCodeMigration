namespace LegacyProject.Models;

/// <summary>
/// Represents a physical or postal address.
/// Used for billing and shipping.
/// </summary>
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    /// <summary>Returns the full one-line address string.</summary>
    public string GetFullAddress() =>
        $"{Street}, {City}, {State} {ZipCode}, {Country}";

    /// <summary>Returns true if all mandatory fields are populated.</summary>
    public bool IsComplete() =>
        !string.IsNullOrWhiteSpace(Street) &&
        !string.IsNullOrWhiteSpace(City) &&
        !string.IsNullOrWhiteSpace(Country);
}
