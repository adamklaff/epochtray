# v0.1 validation

Built with the .NET 10 SDK on Linux using Windows cross-targeting.

- Release solution build: passed, zero warnings and errors.
- Conversion regression suite: 62 tests passed, including 1,000 randomized nanosecond round trips.
- Self-contained single-file Windows x64 publish: passed.
- Project XML and all eight embedded icon resolutions: checked.
- Git whitespace check: passed.

The Windows desktop app could not be launched in this Linux environment. Tray, clipboard, startup registration, and layout behavior must still be exercised on a Windows desktop. The GitHub workflow runs the conversion suite and builds portable x64 and ARM64 executables on Windows once these files are pushed.

## Windows smoke test

1. Launch the executable; confirm a clock icon appears in the notification area.
2. Left-click it, convert `1790965366`, and verify `2026-10-02T18:22:46Z`.
3. Convert `2026-10-02T11:22:46.123456789-07:00`; verify nanoseconds `1790965366123456789`.
4. Copy each result, paste back where appropriate, and check exact round trips.
5. Switch date-input zone and 12/24-hour display; close, reopen, and restart to check persistence.
6. Put a timestamp on the clipboard and reopen; repeat with unrelated text and with clipboard inspection disabled.
7. Try a negative epoch and both invalid and ambiguous local DST dates.
8. Verify right-click timestamp copy actions and Start with Windows on/off, then sign in again to test autostart.
9. Verify close/Esc hides the form, a second launch does not add another tray icon, and Exit removes the icon.
10. Check layout and copy buttons at 100%, 150%, and 200% scaling, including a secondary monitor and a moved taskbar.
