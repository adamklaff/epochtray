namespace EpochTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\EpochTray.SingleInstance", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("EpochTray is already running. Click its icon in the notification area.", "EpochTray");
            return;
        }
        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext();
        Application.Run(context);
    }
}
