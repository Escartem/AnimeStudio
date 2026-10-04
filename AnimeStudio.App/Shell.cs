using System;
using System.Diagnostics;
using System.IO;

namespace AnimeStudio.App
{
    public static class Shell
    {
        public static void OpenFolder(string path) => Open(path);

        public static void OpenUrl(string url) => Open(url);

        public static void RevealFile(string path)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    Process.Start("explorer.exe", $"/select, \"{path}\"");
                else if (OperatingSystem.IsMacOS())
                    Process.Start("open", new[] { "-R", path });
                else
                    Open(Path.GetDirectoryName(path));
            }
            catch (Exception e)
            {
                Logger.Warning($"Unable to reveal {path}: {e.Message}");
            }
        }

        private static void Open(string target)
        {
            try
            {
                if (OperatingSystem.IsLinux())
                    Process.Start("xdg-open", new[] { target });
                else
                    Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                Logger.Warning($"Unable to open {target}: {e.Message}");
            }
        }
    }
}
