using System.Globalization;
using System.Numerics;

namespace EpochTray.Core;

public enum TimestampUnit { Auto, Seconds, Milliseconds, Microseconds, Nanoseconds }

/// <summary>An exact instant, retaining the nanoseconds below DateTime's 100 ns resolution.</summary>
public sealed record EpochInstant
{
    public BigInteger UnixNanoseconds { get; }
    public DateTimeOffset Utc { get; }
    public int SubTickNanoseconds { get; }

    public EpochInstant(BigInteger unixNanoseconds)
    {
        var ticks = EpochConverter.FloorDivide(unixNanoseconds, 100);
        var absoluteTicks = ticks + DateTimeOffset.UnixEpoch.Ticks;
        if (absoluteTicks < DateTimeOffset.MinValue.Ticks || absoluteTicks > DateTimeOffset.MaxValue.Ticks)
            throw new FormatException("The timestamp is outside the supported date range (years 0001–9999).");
        UnixNanoseconds = unixNanoseconds;
        Utc = new DateTimeOffset((long)absoluteTicks, TimeSpan.Zero);
        SubTickNanoseconds = (int)(unixNanoseconds - ticks * 100);
    }

    public static EpochInstant FromDateTimeOffset(DateTimeOffset value) =>
        new((BigInteger)(value.UtcTicks - DateTimeOffset.UnixEpoch.Ticks) * 100);

    public string Timestamp(TimestampUnit unit) =>
        EpochConverter.FloorDivide(UnixNanoseconds, EpochConverter.NanosecondsPerUnit(unit))
            .ToString(CultureInfo.InvariantCulture);

    public string Iso8601()
    {
        var fraction = (Utc.Ticks % TimeSpan.TicksPerSecond) * 100 + SubTickNanoseconds;
        var suffix = fraction == 0 ? "" : "." + fraction.ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
        return Utc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + suffix + "Z";
    }

    public string Display(TimeZoneInfo zone, bool use24HourClock)
    {
        var date = TimeZoneInfo.ConvertTime(Utc, zone);
        var clock = use24HourClock ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd hh:mm:ss tt";
        var fraction = (Utc.Ticks % TimeSpan.TicksPerSecond) * 100 + SubTickNanoseconds;
        var fractionalText = fraction == 0 ? "" : "." + fraction.ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
        // Keep fractional seconds adjacent to the seconds even in a 12-hour display.
        var format = use24HourClock ? clock : "yyyy-MM-dd hh:mm:ss";
        var text = date.ToString(format, CultureInfo.InvariantCulture) + fractionalText;
        if (!use24HourClock) text += date.ToString(" tt", CultureInfo.InvariantCulture);
        return text + (zone.Equals(TimeZoneInfo.Utc) ? " UTC" : " " + date.ToString("zzz", CultureInfo.InvariantCulture));
    }
}

public static class EpochConverter
{
    public static (EpochInstant Instant, TimestampUnit Unit) FromUnixTimestamp(
        string input, TimestampUnit unit = TimestampUnit.Auto)
    {
        input = input.Trim();
        if (input.Length == 0 || input.Length > 40 || !IsInteger(input) ||
            !BigInteger.TryParse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            throw new FormatException("Enter a signed integer Unix timestamp.");
        if (unit == TimestampUnit.Auto)
        {
            var digits = input.TrimStart('+', '-').TrimStart('0').Length;
            unit = digits <= 10 ? TimestampUnit.Seconds : digits <= 13 ? TimestampUnit.Milliseconds
                : digits <= 16 ? TimestampUnit.Microseconds : TimestampUnit.Nanoseconds;
        }
        return (new EpochInstant(value * NanosecondsPerUnit(unit)), unit);
    }

    public static bool IsInteger(string input)
    {
        var span = input.AsSpan().Trim();
        if (span.Length > 0 && span[0] is '+' or '-') span = span[1..];
        if (span.Length == 0) return false;
        foreach (var c in span) if (c is < '0' or > '9') return false;
        return true;
    }

    internal static BigInteger FloorDivide(BigInteger value, BigInteger divisor)
    {
        var quotient = BigInteger.DivRem(value, divisor, out var remainder);
        return remainder.Sign < 0 ? quotient - 1 : quotient;
    }

    internal static BigInteger NanosecondsPerUnit(TimestampUnit unit) => unit switch
    {
        TimestampUnit.Seconds => 1_000_000_000,
        TimestampUnit.Milliseconds => 1_000_000,
        TimestampUnit.Microseconds => 1_000,
        TimestampUnit.Nanoseconds => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };
}
