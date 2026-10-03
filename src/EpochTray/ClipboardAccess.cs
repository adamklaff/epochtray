using System.Runtime.InteropServices;

namespace EpochTray;

internal static class ClipboardAccess
{
    public static string? ReadText()
    {
        try { return Clipboard.ContainsText() ? Clipboard.GetText().Trim() : null; }
        catch (ExternalException) { return null; }
    }

    public static bool Copy(string text)
    {
        try { Clipboard.SetText(text); return true; }
        catch (ExternalException) { return false; }
    }
}
