using System.Globalization;

namespace TenSecondTom.Shared.Extensions;

/// <summary>
/// Helper extensions for converting and formatting <see cref="DateTimeOffset"/> values.
/// Centralizes the "user local" concept so timestamps are displayed consistently.
/// </summary>
public static class DateTimeOffsetExtensions
{
    /// <summary>
    /// Converts a timestamp to the user's local timezone using <see cref="TimeZoneInfo.Local"/>.
    /// </summary>
    public static DateTimeOffset ToUserLocalTime(this DateTimeOffset value)
    {
        var localZone = TimeZoneInfo.Local;
        return TimeZoneInfo.ConvertTime(value, localZone);
    }

    /// <summary>
    /// Converts and formats a timestamp using the application's preferred display conventions.
    /// </summary>
    public static string ToUserLocalDisplayString(
        this DateTimeOffset value,
        string format,
        IFormatProvider? formatProvider = null)
    {
        var localValue = value.ToUserLocalTime();
        return localValue.ToString(format, formatProvider ?? CultureInfo.InvariantCulture);
    }
}
