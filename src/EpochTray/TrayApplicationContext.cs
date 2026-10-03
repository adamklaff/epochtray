using System.Globalization;
using System.Reflection;

namespace EpochTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon tray;
    private readonly ConverterForm form;
    private readonly ContextMenuStrip menu;
    private readonly Icon icon;
    private readonly ToolStripMenuItem startup;

    public TrayApplicationContext()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EpochTray.Assets.epochtray.ico")
            ?? throw new InvalidOperationException("The application icon is missing.");
        icon = new Icon(stream);
        form = new ConverterForm(Settings.Load(), icon);
        menu = new ContextMenuStrip();
        menu.Items.Add("Open converter", null, (_, _) => form.Open());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Copy current seconds", null, (_, _) => CopyCurrent(false));
        menu.Items.Add("Copy current milliseconds", null, (_, _) => CopyCurrent(true));
        menu.Items.Add(new ToolStripSeparator());
        startup = new ToolStripMenuItem("Start with Windows");
        startup.Click += (_, _) => ToggleStartup();
        menu.Items.Add(startup);
        menu.Items.Add("Settings", null, (_, _) => form.Open());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        menu.Opening += (_, _) =>
        {
            try { startup.Checked = StartupRegistration.IsEnabled(); }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException) { startup.Enabled = false; }
        };
        tray = new NotifyIcon { Icon = icon, Text = "EpochTray — Unix timestamp converter", ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) form.Open(); };
    }

    private void CopyCurrent(bool milliseconds)
    {
        var now = DateTimeOffset.UtcNow;
        var value = milliseconds ? now.ToUnixTimeMilliseconds() : now.ToUnixTimeSeconds();
        if (!ClipboardAccess.Copy(value.ToString(CultureInfo.InvariantCulture)))
            MessageBox.Show("The clipboard is busy. Try again.", "EpochTray");
    }

    private void ToggleStartup()
    {
        try
        {
            StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled());
            startup.Checked = StartupRegistration.IsEnabled();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            MessageBox.Show("Windows did not allow the startup preference to be changed.", "EpochTray");
        }
    }

    protected override void ExitThreadCore()
    {
        tray.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { tray.Dispose(); menu.Dispose(); form.Dispose(); icon.Dispose(); }
        base.Dispose(disposing);
    }
}
