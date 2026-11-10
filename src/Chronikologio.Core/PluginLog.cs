namespace Chronikologio.Core;

/// <summary>
/// A small log of what the Opsis plugin does (start, feed registered, snapshot ready, lines asked for), written to %AppData%\Chronikologio\plugin.log, so that
/// "the feed does not appear" can be answered from facts. The file is cut when it grows over 200 KB. Failing to write never disturbs the plugin.
/// </summary>
public static class PluginLog
{
    private static readonly object Gate = new();

    public static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronikologio", "plugin.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var path = Path;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > 200_000) File.Delete(path);
                File.AppendAllText(path, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { }
    }
}
