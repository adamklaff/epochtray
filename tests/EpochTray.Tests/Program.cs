using System.Globalization;
using System.Numerics;
using EpochTray.Core;

// Dependency-free regression suite. Returns a nonzero exit code on any failure.
var passed = 0;
var failed = 0;
void Test(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}; received {actual}.");
}
void Reject(Action action)
{
    try { action(); }
    catch (FormatException) { return; }
    throw new Exception("Expected a FormatException.");
}
EpochInstant Unix(string text, TimestampUnit unit = TimestampUnit.Auto) => EpochConverter.FromUnixTimestamp(text, unit).Instant;
EpochInstant Date(string text, TimeZoneInfo? zone = null) => DateParser.Parse(text, zone ?? TimeZoneInfo.Utc, CultureInfo.InvariantCulture);

Test("Unix epoch", () => Equal("1970-01-01T00:00:00Z", Unix("0").Iso8601()));
Test("Seconds", () => Equal("2026-10-02T18:22:46Z", Unix("1790965366").Iso8601()));
Test("Milliseconds", () => Equal("2026-10-02T18:22:46.123Z", Unix("1790965366123").Iso8601()));
Test("Microseconds", () => Equal("2026-10-02T18:22:46.123456Z", Unix("1790965366123456").Iso8601()));
Test("Nanoseconds", () => Equal("2026-10-02T18:22:46.123456789Z", Unix("1790965366123456789").Iso8601()));
Test("Nanoseconds exact round trip", () => Equal("1790965366123456789", Date(Unix("1790965366123456789").Iso8601()).Timestamp(TimestampUnit.Nanoseconds)));
Test("Negative second", () => Equal("1969-12-31T23:59:59Z", Unix("-1").Iso8601()));
Test("Negative nanosecond", () => Equal("1969-12-31T23:59:59.999999999Z", Unix("-1", TimestampUnit.Nanoseconds).Iso8601()));
Test("Negative units round down", () =>
{
    var instant = Unix("-1", TimestampUnit.Nanoseconds);
    Equal("-1", instant.Timestamp(TimestampUnit.Seconds));
    Equal("-1", instant.Timestamp(TimestampUnit.Milliseconds));
    Equal("-1", instant.Timestamp(TimestampUnit.Microseconds));
});
Test("Positive units round down", () => Equal("1790965366", Unix("1790965366999999999").Timestamp(TimestampUnit.Seconds)));
Test("Signed timestamp with padding", () => Equal("2026-10-02T18:22:46Z", Unix("  +0001790965366  ").Iso8601()));
Test("Explicit unit near epoch", () => Equal("1970-01-01T00:00:00.001Z", Unix("1", TimestampUnit.Milliseconds).Iso8601()));
foreach (var (text, unit) in new[] {
    ("1790965366", TimestampUnit.Seconds), ("1790965366000", TimestampUnit.Milliseconds),
    ("1790965366000000", TimestampUnit.Microseconds), ("1790965366000000000", TimestampUnit.Nanoseconds) })
    Test("Detect " + unit, () => Equal(unit, EpochConverter.FromUnixTimestamp(text).Unit));
foreach (var text in new[] { "", "hello", "+", "1.5", "1e9", "1,000", "１２３", new string('9', 41), "999999999999999999999999999999999999999" })
    Test("Reject timestamp " + text, () => Reject(() => Unix(text)));
Test("Minimum date", () => Equal("0001-01-01T00:00:00Z", Date("0001-01-01T00:00:00Z").Iso8601()));
Test("Maximum nanosecond date", () => Equal("9999-12-31T23:59:59.999999999Z", Date("9999-12-31T23:59:59.999999999Z").Iso8601()));
Test("Below minimum", () => Reject(() => new EpochInstant(Date("0001-01-01T00:00:00Z").UnixNanoseconds - 1)));
Test("Above maximum", () => Reject(() => new EpochInstant(Date("9999-12-31T23:59:59.999999999Z").UnixNanoseconds + 1)));
Test("Beyond long nanoseconds", () => Equal("2500-01-01T00:00:00Z", Unix(Date("2500-01-01T00:00:00Z").Timestamp(TimestampUnit.Nanoseconds), TimestampUnit.Nanoseconds).Iso8601()));
Test("ISO with negative offset", () => Equal("1790965366", Date("2026-10-02T11:22:46-07:00").Timestamp(TimestampUnit.Seconds)));
Test("ISO with positive offset", () => Equal("2026-10-02T18:22:46Z", Date("2026-10-03T00:07:46+05:45").Iso8601()));
Test("ISO compact offset", () => Equal("2026-10-02T18:22:46Z", Date("2026-10-02T11:22:46-0700").Iso8601()));
Test("ISO lowercase z", () => Equal("2026-10-02T18:22:46Z", Date("2026-10-02t18:22:46z").Iso8601()));
Test("ISO fractional date", () => Equal("1790965366123456789", Date("2026-10-02T11:22:46.123456789-07:00").Timestamp(TimestampUnit.Nanoseconds)));
Test("Naive UTC date", () => Equal("1790965366", Date("2026-10-02 18:22:46").Timestamp(TimestampUnit.Seconds)));
var fixedZone = TimeZoneInfo.CreateCustomTimeZone("Test-7", TimeSpan.FromHours(-7), "Test-7", "Test-7");
Test("Naive local date", () => Equal("1790965366", Date("2026-10-02 11:22:46", fixedZone).Timestamp(TimestampUnit.Seconds)));
Test("Explicit offset overrides input zone", () => Equal("1790965366", Date("2026-10-02T18:22:46Z", fixedZone).Timestamp(TimestampUnit.Seconds)));
Test("Minute precision", () => Equal("2026-10-02T18:22:00Z", Date("2026-10-02T18:22Z").Iso8601()));
Test("Reject excessive fractional precision", () => Reject(() => Date("2026-10-02T18:22:46.1234567891Z")));
Test("Reject fraction without seconds", () => Reject(() => Date("2026-10-02T18:22.123Z")));
Test("RFC 1123", () => Equal("2026-10-02T18:22:46Z", Date("Fri, 02 Oct 2026 18:22:46 GMT").Iso8601()));
Test("US culture", () => Equal("2026-10-02T18:22:46Z", DateParser.Parse("10/2/2026 6:22:46 PM", TimeZoneInfo.Utc, CultureInfo.GetCultureInfo("en-US")).Iso8601()));
Test("UK culture", () => Equal("2026-10-02T18:22:46Z", DateParser.Parse("02/10/2026 18:22:46", TimeZoneInfo.Utc, CultureInfo.GetCultureInfo("en-GB")).Iso8601()));
foreach (var text in new[] { "not a date", "2026-02-30T00:00:00Z", "2026-10-02T25:00:00Z", "2026-10-02T18:22:60Z", "2026-10-02T18:22:46+15:00", "2026-10-02T18:22:46+01:60", "2026-10-02T18:22:46+14:01", "0001-01-01T00:00:00+01:00", new string('x', 257) })
    Test("Reject date " + text[..Math.Min(text.Length, 50)], () => Reject(() => Date(text)));
Test("24-hour display", () => Equal("2026-10-02 18:22:46.123456789 UTC", Unix("1790965366123456789").Display(TimeZoneInfo.Utc, true)));
Test("12-hour display", () => Equal("2026-10-02 06:22:46.123456789 PM UTC", Unix("1790965366123456789").Display(TimeZoneInfo.Utc, false)));
Test("Local display includes offset", () => Equal("2026-10-02 11:22:46 -07:00", Unix("1790965366").Display(fixedZone, true)));
var pacific = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Pacific Standard Time" : "America/Los_Angeles");
Test("Reject spring DST gap", () => Reject(() => Date("2026-03-08 02:30:00", pacific)));
Test("Reject autumn DST overlap", () => Reject(() => Date("2026-11-01 01:30:00", pacific)));
Test("DST overlap with offset", () => Equal("2026-11-01T08:30:00Z", Date("2026-11-01T01:30:00-07:00", pacific).Iso8601()));
Test("DST second occurrence", () => Equal("2026-11-01T09:30:00Z", Date("2026-11-01T01:30:00-08:00", pacific).Iso8601()));
Test("DateTimeOffset preserves ticks", () =>
{
    var date = DateTimeOffset.UnixEpoch.AddTicks(-1);
    Equal("-100", EpochInstant.FromDateTimeOffset(date).Timestamp(TimestampUnit.Nanoseconds));
});
Test("Many nanosecond round trips", () =>
{
    var random = new Random(2026);
    for (var i = 0; i < 1000; i++)
    {
        var nanos = (BigInteger)random.NextInt64(-62_135_596_800, 253_402_300_799) * 1_000_000_000 + random.Next(1_000_000_000);
        Equal(nanos, Date(new EpochInstant(nanos).Iso8601()).UnixNanoseconds);
    }
});
Console.WriteLine($"\n{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;
