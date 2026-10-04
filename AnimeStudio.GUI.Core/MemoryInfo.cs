using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AnimeStudio.GUI.Core
{
    public static partial class MemoryInfo
    {
        // Free physical memory in bytes, null when it can't be determined.
        public static ulong? GetAvailablePhysicalMemory()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    return GetWindows();
                if (OperatingSystem.IsLinux())
                    return GetLinux();
                if (OperatingSystem.IsMacOS())
                    return GetMacOS();
            }
            catch (Exception e)
            {
                Logger.Verbose($"Unable to query free memory : {e.Message}");
            }
            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        private static ulong? GetWindows()
        {
            var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            return GlobalMemoryStatusEx(ref status) ? status.ullAvailPhys : null;
        }

        private static ulong? GetLinux()
        {
            var line = File.ReadLines("/proc/meminfo").FirstOrDefault(x => x.StartsWith("MemAvailable:"));
            if (line == null)
                return null;
            var kb = ulong.Parse(Regex.Match(line, @"\d+").Value);
            return kb * 1024;
        }

        // vm_stat reports pages; free + inactive + speculative is what macOS can hand out without swapping.
        private static ulong? GetMacOS()
        {
            using var process = Process.Start(new ProcessStartInfo("vm_stat") { RedirectStandardOutput = true, UseShellExecute = false });
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);

            var pageSize = ulong.Parse(Regex.Match(output, @"page size of (\d+) bytes").Groups[1].Value);
            ulong Pages(string name)
            {
                var match = Regex.Match(output, $@"{name}:\s+(\d+)");
                return match.Success ? ulong.Parse(match.Groups[1].Value) : 0;
            }
            return (Pages("Pages free") + Pages("Pages inactive") + Pages("Pages speculative")) * pageSize;
        }
    }
}
