using LegacyProject.Models.Enums;

namespace LegacyProject.Helpers;

/// <summary>
/// Static utility class for pricing, discount, tax, and shipping calculations.
/// </summary>
public static class PriceCalculator
{
    /// <summary>
    /// Calculates the per-unit discount amount for a given customer tier.
    /// </summary>
    public static decimal CalculateDiscount(decimal unitPrice, CustomerType customerType)
    {
        var rate = customerType switch
        {
            CustomerType.Premium   => 0.10m,
            CustomerType.Corporate => 0.15m,
            CustomerType.VIP       => 0.20m,
            _                      => 0.00m
        };
        return unitPrice * rate;
    }

    /// <summary>Calculates tax for the given sub-total.</summary>
    public static decimal CalculateTax(decimal subTotal, decimal taxRate = 0.18m) =>
        subTotal * taxRate;

    /// <summary>
    /// Returns the shipping charge for an order.
    /// Free for orders over $1,000; express shipping is $150 otherwise; standard is $50.
    /// </summary>
    public static decimal CalculateShipping(decimal orderTotal, bool isExpress = false)
    {
        if (orderTotal >= 1000m) return 0m;
        return isExpress ? 150m : 50m;
    }

    /// <summary>Rounds a decimal value to two decimal places (banker's rounding avoidance).</summary>
    public static decimal RoundToTwoDecimals(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Applies a percentage markup to a base price.</summary>
    public static decimal ApplyMarkup(decimal basePrice, decimal markupPercent) =>
        basePrice * (1 + markupPercent / 100m);
}
