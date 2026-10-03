# EpochTray

A small Windows system-tray utility for converting Unix timestamps and dates without opening a browser. C# / .NET 10 / Windows Forms. MIT licensed.

## Run

Extract the portable Windows build, keep `EpochTray.exe` in a permanent folder, and double-click it. No installation or .NET runtime download is required for the self-contained build.

EpochTray starts in the notification area. Windows may put its clock icon in the hidden-icons menu; drag it onto the taskbar notification area for quick access.

- **Left-click** the icon to open the converter.
- **Right-click** for current timestamp copy actions, Start with Windows, settings, or Exit.
- Closing the converter or pressing **Esc** hides it; **Exit** ends the app.
- A second launch tells you the app is already running.

The executable is unsigned. Windows may show a reputation warning for a newly downloaded build.

## Convert

Paste a Unix integer or a date into the single input. Results update as you type; Enter also converts. Each result has a Copy button.

| Input | Result in UTC |
| --- | --- |
| `1790965366` | `2026-10-02T18:22:46Z` |
| `1790965366123` | `2026-10-02T18:22:46.123Z` |
| `1790965366123456` | `2026-10-02T18:22:46.123456Z` |
| `1790965366123456789` | `2026-10-02T18:22:46.123456789Z` |
| `2026-10-02T11:22:46-07:00` | `1790965366` seconds |

The converter shows UTC, local time with its UTC offset, ISO 8601, seconds, milliseconds, microseconds, and nanoseconds. A separate live clock and tray actions let you copy the current timestamp without changing the conversion.

### Timestamp units and precision

Auto detection uses the number of significant digits, excluding the sign and leading zeroes: up to 10 = seconds, 11–13 = milliseconds, 14–16 = microseconds, and 17 or more = nanoseconds. This is a convenience for contemporary timestamps, not a universal way to identify units. **Select the unit explicitly for dates close to 1970, dates far in the future, or any ambiguous value.**

Only integer timestamps are accepted. Signed values and dates before 1970 work. All arithmetic uses integers; nanoseconds survive round trips without floating-point loss. Outputs in coarser units round down, including negative values: `-1` nanosecond produces `-1` second, millisecond, and microsecond.

Supported UTC dates span years 0001–9999. Nanoseconds use `BigInteger`, so future dates are not constrained by the signed 64-bit nanosecond limit. Leap seconds are not represented.

### Date parsing

- ISO 8601: `2026-10-02T18:22:46Z`, `2026-10-02T11:22:46-07:00`, and fractions of up to nine digits.
- A space may replace `T`, and seconds may be omitted.
- Common human-readable dates use your Windows regional format, including date-only inputs at midnight and RFC 1123 dates. Numeric day/month ordering therefore follows your region; ISO dates avoid ambiguity.
- Dates without an explicit offset use the **Local time / UTC** selector. Explicit offsets always override it.
- Local times in a daylight-saving gap or overlap are rejected. Enter an explicit offset to identify the intended instant.
- Natural-language relative dates and EpochConverter's secondary calculators are outside v0.1's scope.

### Clipboard and preferences

By default, opening the converter inspects text on the clipboard once. If it recognizes a timestamp or date, it displays the conversion; otherwise it keeps the previous input. This can be disabled with **Read clipboard when opening**. Clipboard contents are never sent anywhere, and the app makes no network requests.

The time zone selector, 12/24-hour clock preference, and clipboard preference are saved to `%LOCALAPPDATA%\EpochTray\settings.json`. Start with Windows uses your user's Windows Run registry key, requires no administrator rights, and points to the executable's current location. Toggle it again after moving the executable; disable it before deleting the app.

## Build from source

Install the .NET 10 SDK. On Windows, from the repo root:

```powershell
dotnet build EpochTray.sln -c Release
dotnet run --project tests/EpochTray.Tests -c Release
dotnet run --project src/EpochTray
```

The dependency-free test runner exits nonzero on failures. It exercises exact precision, negative epochs, all units, range limits, ISO and regional date parsing, explicit offsets, daylight-saving transitions, and 1,000 randomized nanosecond round trips. Use `dotnet run` for this suite, rather than `dotnet test`.

The conversion core and tests also run on Linux and macOS. The tray application requires Windows; it can be cross-compiled with the Windows targeting pack.

### Make a portable executable

```powershell
./scripts/publish.ps1
# ARM64 Windows:
./scripts/publish.ps1 -Runtime win-arm64
```

Or publish directly:

```powershell
dotnet publish src/EpochTray/EpochTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/win-x64
```

The output is `artifacts/win-x64/EpochTray.exe`. The self-contained executable includes the .NET desktop runtime, so its download is larger than the source project.

GitHub Actions builds the solution, runs the regression suite on Windows, and uploads portable x64 and ARM64 builds on pushes, pull requests, and manual runs. Download them from the workflow's **Artifacts** section.

## Project layout

- `src/EpochTray.Core`: platform-independent integer conversion and date parsing.
- `src/EpochTray`: tray lifecycle, converter form, clipboard, preferences, and startup registration.
- `tests/EpochTray.Tests`: executable regression suite with no third-party test dependencies.
- `.github/workflows/build.yml`: Windows verification and portable build artifacts.
- `scripts/publish.ps1`: local build, test, and publish helper.

Before a release, check on Windows: tray left/right clicks, copy/paste, closing and reopening, Exit removing the icon, saved preferences, startup toggling, and sizing at 100%/150%/200% display scaling with multiple monitors. Cross-compilation does not verify desktop interactions.
