namespace LegacyProject.Helpers;

/// <summary>
/// Static utility class for date and time operations used across the application.
/// </summary>
public static class DateHelper
{
    /// <summary>Returns a compact UTC timestamp string suitable for order/invoice numbers.</summary>
    public static string CurrentTimestamp() =>
        DateTime.UtcNow.ToString("yyyyMMddHHmmss");

    /// <summary>Formats a date using the specified format string.</summary>
    public static string FormatDate(DateTime date, string format = "yyyy-MM-dd") =>
        date.ToString(format);

    /// <summary>Returns the number of whole days between two dates.</summary>
    public static int DaysBetween(DateTime start, DateTime end) =>
        (int)(end - start).TotalDays;

    /// <summary>True if the given date falls on a weekday (Mon–Fri).</summary>
    public static bool IsBusinessDay(DateTime date) =>
        date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;

    /// <summary>Returns the next weekday after the given date.</summary>
    public static DateTime GetNextBusinessDay(DateTime from)
    {
        var next = from.AddDays(1);
        while (!IsBusinessDay(next))
            next = next.AddDays(1);
        return next;
    }

    /// <summary>Returns the beginning of the day (midnight) for the given date.</summary>
    public static DateTime StartOfDay(DateTime date) =>
        new(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Returns the end of the day (23:59:59) for the given date.</summary>
    public static DateTime EndOfDay(DateTime date) =>
        new(date.Year, date.Month, date.Day, 23, 59, 59, DateTimeKind.Utc);
}
