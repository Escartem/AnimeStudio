using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AnimeStudio.PInvoke
{
    public static class DllLoader
    {
        public static string GetLibraryFileName(string logicalName, OSPlatform platform)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);

            if (platform == OSPlatform.Windows)
            {
                return $"{logicalName}.dll";
            }

            if (platform == OSPlatform.Linux)
            {
                return $"lib{logicalName}.so";
            }

            throw new PlatformNotSupportedException($"Native library naming is not supported for {platform}.");
        }

        public static void PreloadDll(string dllName, bool archSpecific = true)
        {
            var dllDir = GetDirectedDllDirectory(archSpecific);

            // Not using OperatingSystem.Platform.
            // See: https://www.mono-project.com/docs/faq/technical/#how-to-detect-the-execution-platform
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Win32.LoadDll(dllDir, dllName);
            }
            else
            {
                Posix.LoadDll(dllDir, dllName);
            }
        }

        private static string GetDirectedDllDirectory(bool archSpecific)
        {
            var localPath = Process.GetCurrentProcess().MainModule.FileName;
            var localDir = Path.GetDirectoryName(localPath);

            if (Path.Exists(Path.Combine(localDir, "bin")))
            {
                localDir = Path.Combine(localDir, "bin");
            }

            if (!archSpecific)
            {
                return localDir;
            }

            var subDir = Environment.Is64BitProcess ? "x64" : "x86";

            return Path.Combine(localDir, subDir);
        }

        private static partial class Win32
        {

            internal static void LoadDll(string dllDir, string dllName)
            {
                var dllFileName = GetLibraryFileName(dllName, OSPlatform.Windows);
                var directedDllPath = Path.Combine(dllDir, dllFileName);

                // Specify SEARCH_DLL_LOAD_DIR to load dependent libraries located in the same platform-specific directory.
                var hLibrary = LoadLibraryEx(directedDllPath, IntPtr.Zero, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR);

                if (hLibrary == IntPtr.Zero)
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    var exception = new Win32Exception(errorCode);

                    throw new DllNotFoundException(exception.Message, exception);
                }
            }

            // HMODULE LoadLibraryExA(LPCSTR lpLibFileName, HANDLE hFile, DWORD dwFlags);
            // HMODULE LoadLibraryExW(LPCWSTR lpLibFileName, HANDLE hFile, DWORD dwFlags);
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);

            private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x1000;
            private const uint LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x100;

        }

        private static class Posix
        {

            internal static void LoadDll(string dllDir, string dllName)
            {
                OSPlatform platform;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    platform = OSPlatform.Linux;
                }
                else
                {
                    throw new NotSupportedException();
                }

                var dllFileName = GetLibraryFileName(dllName, platform);
                var directedDllPath = Path.Combine(dllDir, dllFileName);

                const int ldFlags = RTLD_NOW | RTLD_GLOBAL;
                var hLibrary = DlOpen(directedDllPath, ldFlags);

                if (hLibrary == IntPtr.Zero)
                {
                    var pErrStr = DlError();
                    // `PtrToStringAnsi` always uses the specific constructor of `String` (see dotnet/core#2325),
                    // which in turn interprets the byte sequence with system default codepage. On OSX and Linux
                    // the codepage is UTF-8 so the error message should be handled correctly.
                    var errorMessage = Marshal.PtrToStringAnsi(pErrStr);

                    throw new DllNotFoundException(errorMessage);
                }
            }

            // OSX and most Linux OS use LP64 so `int` is still 32-bit even on 64-bit platforms.
            // void *dlopen(const char *filename, int flag);
            [DllImport("libdl", EntryPoint = "dlopen")]
            private static extern IntPtr DlOpen([MarshalAs(UnmanagedType.LPStr)] string fileName, int flags);

            // char *dlerror(void);
            [DllImport("libdl", EntryPoint = "dlerror")]
            private static extern IntPtr DlError();

            private const int RTLD_LAZY = 0x1;
            private const int RTLD_NOW = 0x2;
            private const int RTLD_GLOBAL = 0x100;

        }

    }
}
