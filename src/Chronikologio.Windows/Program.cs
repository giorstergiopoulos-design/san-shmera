using Chronikologio.UI;

namespace Chronikologio.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var window = new MainWindow(Path.Combine(AppContext.BaseDirectory, "data"));
        // the icon of the executable (chronikologio.ico) for the title bar and the taskbar
        try { if (Environment.ProcessPath is { } exe) window.Icon = Icon.ExtractAssociatedIcon(exe); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { }
        Application.Run(window);
    }
}
