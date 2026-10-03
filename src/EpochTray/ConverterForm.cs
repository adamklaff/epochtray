using System.Globalization;
using EpochTray.Core;

namespace EpochTray;

internal sealed class ConverterForm : Form
{
    private readonly Settings settings;
    private readonly TextBox input = new() { Dock = DockStyle.Fill, PlaceholderText = "Unix timestamp or date / time", MaxLength = 256 };
    private readonly ComboBox units = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox inputZone = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly Label status = new() { AutoSize = true, ForeColor = Color.FromArgb(79, 94, 112), MaximumSize = new Size(540, 0) };
    private readonly Label liveEpoch = new() { AutoSize = true, Font = new Font("Consolas", 13) };
    private readonly Dictionary<string, TextBox> outputs = new();
    private readonly Dictionary<string, Button> copyButtons = new();
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 1000 };
    private EpochInstant? instant;
    private bool initializing = true;

    public ConverterForm(Settings settings, Icon icon)
    {
        this.settings = settings;
        Text = "EpochTray";
        Icon = icon;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(247, 249, 252);
        ClientSize = new Size(610, 680);
        MinimumSize = new Size(570, 600);
        MaximizeBox = false;

        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22),
            ColumnCount = 1, RowCount = 0
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(page);
        void Add(Control control)
        {
            var row = page.RowCount++;
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Margin = new Padding(0, 0, 0, 12);
            page.Controls.Add(control, 0, row);
        }

        Add(new Label { Text = "EpochTray", Font = new Font("Segoe UI", 22, FontStyle.Bold), AutoSize = true });
        Add(new Label { Text = "Unix timestamps ↔ dates. Paste, convert, copy.", AutoSize = true });
        var nowPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
        nowPanel.Controls.Add(new Label { Text = "NOW", AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
        nowPanel.Controls.Add(liveEpoch);
        nowPanel.Controls.Add(ActionButton("Copy seconds", () => Copy(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))));
        nowPanel.Controls.Add(ActionButton("Use now", () => SetInput(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))));
        Add(nowPanel);
        Add(new Label { Text = "TIMESTAMP OR DATE", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        input.Font = new Font("Consolas", 12);
        input.AccessibleName = "Timestamp or date input";
        Add(input);

        var options = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2 };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        options.Controls.Add(new Label { Text = "Timestamp unit", AutoSize = true }, 0, 0);
        options.Controls.Add(new Label { Text = "Dates without an offset", AutoSize = true }, 1, 0);
        units.Items.AddRange(["Auto detect", "Seconds", "Milliseconds", "Microseconds", "Nanoseconds"]);
        units.SelectedIndex = 0;
        units.AccessibleName = "Timestamp unit";
        inputZone.Items.AddRange(["Local time", "UTC"]);
        inputZone.SelectedIndex = settings.UseUtcInput ? 1 : 0;
        inputZone.Dock = DockStyle.Fill;
        inputZone.AccessibleName = "Time zone for dates without an offset";
        options.Controls.Add(units, 0, 1);
        options.Controls.Add(inputZone, 1, 1);
        Add(options);

        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        var convert = ActionButton("Convert", ConvertInput);
        AcceptButton = convert;
        actions.Controls.Add(convert);
        actions.Controls.Add(ActionButton("Paste", () =>
        {
            var text = ClipboardAccess.ReadText();
            if (text is null) SetStatus("The clipboard has no text, or it is busy.", true);
            else SetInput(text);
        }));
        actions.Controls.Add(ActionButton("Clear", () => { input.Clear(); input.Focus(); }));
        Add(actions);
        Add(status);

        var results = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 3 };
        results.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        results.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        results.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
        foreach (var name in new[] { "UTC", "Local", "ISO 8601", "Seconds", "Milliseconds", "Microseconds", "Nanoseconds" })
        {
            var row = results.RowCount++;
            results.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = new Label { Text = name, AutoSize = true, Anchor = AnchorStyles.Left };
            var value = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Font = new Font("Consolas", 10), BackColor = Color.White, AccessibleName = name + " result" };
            var copy = ActionButton("Copy", () => Copy(value.Text));
            copy.Enabled = false;
            copy.AccessibleName = "Copy " + name;
            outputs.Add(name, value);
            copyButtons.Add(name, copy);
            results.Controls.Add(label, 0, row);
            results.Controls.Add(value, 1, row);
            results.Controls.Add(copy, 2, row);
        }
        Add(results);

        var prefs = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
        var clockPreference = new CheckBox { Text = "24-hour clock", AutoSize = true, Checked = settings.Use24HourClock };
        var clipboardPreference = new CheckBox { Text = "Read clipboard when opening", AutoSize = true, Checked = settings.InspectClipboard };
        clockPreference.CheckedChanged += (_, _) => { settings.Use24HourClock = clockPreference.Checked; SaveSettings(); RenderResults(); };
        clipboardPreference.CheckedChanged += (_, _) => { settings.InspectClipboard = clipboardPreference.Checked; SaveSettings(); };
        prefs.Controls.Add(clockPreference);
        prefs.Controls.Add(clipboardPreference);
        Add(prefs);
        Add(new Label { Text = "Closing this window keeps EpochTray in the tray. Right-click the tray icon to exit.", AutoSize = true, MaximumSize = new Size(540, 0), ForeColor = Color.DimGray });

        input.TextChanged += (_, _) => ConvertInput();
        units.SelectedIndexChanged += (_, _) => ConvertInput();
        inputZone.SelectedIndexChanged += (_, _) =>
        {
            settings.UseUtcInput = inputZone.SelectedIndex == 1;
            SaveSettings();
            ConvertInput();
        };
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { Hide(); e.Handled = true; } };
        VisibleChanged += (_, _) => { if (Visible) clock.Start(); else clock.Stop(); };
        clock.Tick += (_, _) => UpdateClock();
        UpdateClock();
        initializing = false;
        SetStatus("Enter a timestamp or a date. Explicit UTC offsets always take precedence.");
    }

    public void Open()
    {
        if (!Visible)
        {
            // The cursor is next to the clicked tray icon, including on secondary monitors.
            var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
            var x = Math.Clamp(Cursor.Position.X - Width / 2, screen.Left, Math.Max(screen.Left, screen.Right - Width));
            var y = Math.Clamp(Cursor.Position.Y - Height, screen.Top, Math.Max(screen.Top, screen.Bottom - Height));
            Location = new Point(x, y);
        }
        if (settings.InspectClipboard)
        {
            var text = ClipboardAccess.ReadText();
            if (text is { Length: > 0 and <= 256 })
            {
                try
                {
                    _ = Parse(text, TimestampUnit.Auto);
                    SetInput(text);
                }
                catch (FormatException) { /* Leave the previous conversion intact. */ }
            }
        }
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        input.Focus();
        input.SelectAll();
    }

    private void SetInput(string text)
    {
        units.SelectedIndex = 0;
        input.Text = text.Length > input.MaxLength ? text[..input.MaxLength] : text;
        ConvertInput();
    }

    private EpochInstant Parse(string text, TimestampUnit unit) => EpochConverter.IsInteger(text)
        ? EpochConverter.FromUnixTimestamp(text, unit).Instant
        : DateParser.Parse(text, settings.UseUtcInput ? TimeZoneInfo.Utc : TimeZoneInfo.Local);

    private void ConvertInput()
    {
        if (initializing) return;
        if (string.IsNullOrWhiteSpace(input.Text))
        {
            instant = null;
            RenderResults();
            SetStatus("Enter a timestamp or a date.");
            return;
        }
        try
        {
            instant = Parse(input.Text, (TimestampUnit)units.SelectedIndex);
            var description = EpochConverter.IsInteger(input.Text)
                ? "Timestamp unit: " + EpochConverter.FromUnixTimestamp(input.Text, (TimestampUnit)units.SelectedIndex).Unit.ToString().ToLowerInvariant()
                : "Date converted. Times without an offset use " + (settings.UseUtcInput ? "UTC." : TimeZoneInfo.Local.DisplayName + ".");
            RenderResults();
            SetStatus(description);
        }
        catch (FormatException ex)
        {
            instant = null;
            RenderResults();
            SetStatus(ex.Message, true);
        }
    }

    private void RenderResults()
    {
        foreach (var value in outputs.Values) value.Clear();
        foreach (var button in copyButtons.Values) button.Enabled = instant is not null;
        if (instant is null) return;
        outputs["UTC"].Text = instant.Display(TimeZoneInfo.Utc, settings.Use24HourClock);
        outputs["Local"].Text = instant.Display(TimeZoneInfo.Local, settings.Use24HourClock);
        outputs["ISO 8601"].Text = instant.Iso8601();
        foreach (var unit in new[] { TimestampUnit.Seconds, TimestampUnit.Milliseconds, TimestampUnit.Microseconds, TimestampUnit.Nanoseconds })
            outputs[unit.ToString()].Text = instant.Timestamp(unit);
    }

    private void Copy(string value) => SetStatus(ClipboardAccess.Copy(value) ? "Copied to clipboard." : "Clipboard is busy. Try again.");
    private void SaveSettings() { if (!settings.Save()) SetStatus("Could not save preferences. They apply until you exit.", true); }
    private void UpdateClock() => liveEpoch.Text = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    private void SetStatus(string message, bool error = false)
    {
        status.Text = message;
        status.ForeColor = error ? Color.FromArgb(164, 39, 39) : Color.FromArgb(79, 94, 112);
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, 30), FlatStyle = FlatStyle.System, Padding = new Padding(3), Margin = new Padding(3) };
        button.Click += (_, _) => action();
        return button;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) clock.Dispose();
        base.Dispose(disposing);
    }
}
