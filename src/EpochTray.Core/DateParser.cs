using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace EpochTray.Core;

public static partial class DateParser
{
    [GeneratedRegex(@"^(?<date>\d{4}-\d{2}-\d{2})[Tt ](?<time>\d{2}:\d{2}(?::\d{2})?)(?:\.(?<fraction>\d+))?\s*(?<zone>[Zz]|[+-]\d{2}:?\d{2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoPattern();

    [GeneratedRegex(@"(?:[Zz]|[+-]\d{2}:?\d{2}|\b(?:GMT|UTC))$", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetPattern();

    public static EpochInstant Parse(string input, TimeZoneInfo defaultZone, CultureInfo? culture = null)
    {
        input = input.Trim();
        if (input.Length == 0 || input.Length > 256)
            throw new FormatException("Enter a date such as 2026-10-02 11:22:46 or an ISO 8601 date.");
        var match = IsoPattern().Match(input);
        if (match.Success)
        {
            if (match.Groups["fraction"].Length > 9)
                throw new FormatException("Fractional seconds may contain at most nine digits.");
            if (match.Groups["fraction"].Length > 0 && match.Groups["time"].Length != 8)
                throw new FormatException("Include seconds before fractional seconds, such as 18:22:46.123.");
            var text = match.Groups["date"].Value + " " + match.Groups["time"].Value;
            var patterns = new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm" };
            if (!DateTime.TryParseExact(text, patterns, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var wall))
                throw new FormatException("That date or time is invalid.");
            var zoneText = match.Groups["zone"].Value;
            DateTimeOffset date;
            if (zoneText.Length == 0) date = ResolveWallTime(wall, defaultZone);
            else if (zoneText.Equals("Z", StringComparison.OrdinalIgnoreCase)) date = new(wall, TimeSpan.Zero);
            else
            {
                var digits = zoneText[1..].Replace(":", "");
                var hours = int.Parse(digits[..2], CultureInfo.InvariantCulture);
                var minutes = int.Parse(digits[2..], CultureInfo.InvariantCulture);
                if (hours > 14 || minutes > 59 || (hours == 14 && minutes != 0))
                    throw new FormatException("The UTC offset must be between -14:00 and +14:00.");
                var offset = TimeSpan.FromMinutes((hours * 60 + minutes) * (zoneText[0] == '-' ? -1 : 1));
                try { date = new(wall, offset); }
                catch (ArgumentException) { throw new FormatException("The date with that offset is outside the supported range."); }
            }
            var fraction = match.Groups["fraction"].Value;
            var nanos = fraction.Length == 0 ? BigInteger.Zero : BigInteger.Parse(fraction.PadRight(9, '0'), CultureInfo.InvariantCulture);
            return new EpochInstant(EpochInstant.FromDateTimeOffset(date).UnixNanoseconds + nanos);
        }

        culture ??= CultureInfo.CurrentCulture;
        if (OffsetPattern().IsMatch(input))
        {
            // DateTimeOffset handles RFC 1123 and explicit offsets without applying the selected zone.
            var normalized = Regex.Replace(input, @"\bUTC$", "GMT", RegexOptions.IgnoreCase);
            if (DateTimeOffset.TryParse(normalized, culture, DateTimeStyles.AllowWhiteSpaces, out var offsetDate) ||
                DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out offsetDate))
                return EpochInstant.FromDateTimeOffset(offsetDate);
        }
        else if (DateTime.TryParse(input, culture, DateTimeStyles.AllowWhiteSpaces, out var wallDate))
        {
            return EpochInstant.FromDateTimeOffset(ResolveWallTime(wallDate, defaultZone));
        }
        throw new FormatException("Date not recognized. Try YYYY-MM-DD HH:mm:ss or ISO 8601 with a UTC offset.");
    }

    private static DateTimeOffset ResolveWallTime(DateTime value, TimeZoneInfo zone)
    {
        value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(value))
            throw new FormatException("This local time does not exist because the clock moved forward. Supply a UTC offset.");
        if (zone.IsAmbiguousTime(value))
            throw new FormatException("This local time occurs twice because the clock moved back. Supply a UTC offset.");
        try { return new DateTimeOffset(value, zone.GetUtcOffset(value)); }
        catch (ArgumentException) { throw new FormatException("The date is outside the supported range for this time zone."); }
    }
}
