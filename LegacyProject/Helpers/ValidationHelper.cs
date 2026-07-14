using System.Text.RegularExpressions;

namespace LegacyProject.Helpers;

/// <summary>
/// Static utility class that provides input validation helpers.
/// Throws <see cref="ArgumentException"/> on invalid inputs.
/// </summary>
public static class ValidationHelper
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex =
        new(@"^\+?[0-9\s\-\(\)]{7,15}$", RegexOptions.Compiled);

    /// <summary>Validates that the email address is syntactically correct.</summary>
    /// <exception cref="ArgumentException">Thrown for invalid email.</exception>
    public static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            throw new ArgumentException($"Invalid email address: '{email}'.");
    }

    /// <summary>Validates that the value is not null or whitespace.</summary>
    /// <exception cref="ArgumentException">Thrown when the value is empty.</exception>
    public static void ValidateNotEmpty(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"'{fieldName}' cannot be null or empty.");
    }

    /// <summary>Validates that a decimal value is strictly positive.</summary>
    /// <exception cref="ArgumentException">Thrown when value is zero or negative.</exception>
    public static void ValidatePositive(decimal value, string fieldName)
    {
        if (value <= 0)
            throw new ArgumentException($"'{fieldName}' must be greater than zero. Got: {value}.");
    }

    /// <summary>Validates that an integer falls within an inclusive range.</summary>
    /// <exception cref="ArgumentException">Thrown when out of range.</exception>
    public static void ValidateRange(int value, int min, int max, string fieldName)
    {
        if (value < min || value > max)
            throw new ArgumentException(
                $"'{fieldName}' must be between {min} and {max}. Got: {value}.");
    }

    /// <summary>Validates that a GUID is not the empty GUID.</summary>
    /// <exception cref="ArgumentException">Thrown for empty GUIDs.</exception>
    public static void ValidateGuid(Guid id, string fieldName)
    {
        if (id == Guid.Empty)
            throw new ArgumentException($"'{fieldName}' must not be an empty GUID.");
    }

    /// <summary>Returns true when a phone number matches the expected format.</summary>
    public static bool IsValidPhone(string phone) =>
        !string.IsNullOrWhiteSpace(phone) && PhoneRegex.IsMatch(phone);
}
