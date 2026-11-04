using System.Runtime.InteropServices;

namespace Chronikologio.UI;

/// <summary>Makes the Windows title bar of a window dark or light to match the theme (Windows 10 build 18985 and later; ignored elsewhere).</summary>
internal static class TitleBar
{
    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(IntPtr handle, bool dark)
    {
        if (handle == IntPtr.Zero) return;
        try
        {
            var value = dark ? 1 : 0;
            DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }
}
