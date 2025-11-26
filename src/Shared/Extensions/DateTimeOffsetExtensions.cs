using System.Globalization;

namespace TenSecondTom.Shared.Extensions;

/// <summary>
/// Helper extensions for converting and formatting <see cref="DateTimeOffset"/> values.
/// Centralizes the "user local" concept so timestamps are displayed consistently.
/// </summary>
/// <remarks>
/// Uses C# 14 extension member syntax for cleaner extension properties.
/// </remarks>
public static class DateTimeOffsetExtensions
{
    extension(DateTimeOffset value)
    {
        /// <summary>
        /// Gets the timestamp converted to the user's local timezone using <see cref="TimeZoneInfo.Local"/>.
        /// </summary>
        public DateTimeOffset UserLocalTime => TimeZoneInfo.ConvertTime(value, TimeZoneInfo.Local);

        /// <summary>
        /// Converts and formats a timestamp using the application's preferred display conventions.
        /// </summary>
        public string ToUserLocalDisplayString(
            string format,
            IFormatProvider? formatProvider = null)
        {
            var localValue = value.UserLocalTime;
            return localValue.ToString(format, formatProvider ?? CultureInfo.InvariantCulture);
        }
    }
}
