// taken from https://github.com/dnSpy/dnSpy/blob/master/Build/AppHostPatcher/Program.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AppHostPatcher
{
    class Program
    {
        static void Usage()
        {
            Console.WriteLine("apphostpatcher <apphostexe> <origdllpath> <newdllpath>");
            Console.WriteLine("apphostpatcher <apphostexe> <newdllpath>");
            Console.WriteLine("apphostpatcher <apphostexe> -d <newsubdir>");
            Console.WriteLine("example: apphostpatcher my.exe -d bin");
        }

        const int maxPathBytes = 1024;

        static string ChangeExecutableExtension(string apphostExe) =>
            // Windows apphosts have an .exe extension. Don't call Path.ChangeExtension() unless it's guaranteed
            // to have an .exe extension, eg. 'some.file' => 'some.file.dll', not 'some.dll'
            apphostExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Path.ChangeExtension(apphostExe, ".dll") : apphostExe + ".dll";

        static string GetPathSeparator(string apphostExe) =>
            apphostExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? @"\" : "/";

        static int Main(string[] args)
        {
            try
            {
                string apphostExe, origPath, newPath;
                if (args.Length == 3)
                {
                    if (args[1] == "-d")
                    {
                        apphostExe = args[0];
                        origPath = Path.GetFileName(ChangeExecutableExtension(apphostExe));
                        newPath = args[2] + GetPathSeparator(apphostExe) + origPath;
                    }
                    else
                    {
                        apphostExe = args[0];
                        origPath = args[1];
                        newPath = args[2];
                    }
                }
                else if (args.Length == 2)
                {
                    apphostExe = args[0];
                    origPath = Path.GetFileName(ChangeExecutableExtension(apphostExe));
                    newPath = args[1];
                }
                else
                {
                    Usage();
                    return 1;
                }
                if (!File.Exists(apphostExe))
                {
                    Console.WriteLine($"Apphost '{apphostExe}' does not exist");
                    return 1;
                }
                if (origPath == string.Empty)
                {
                    Console.WriteLine("Original path is empty");
                    return 1;
                }
                var origPathBytes = Encoding.UTF8.GetBytes(origPath + "\0");
                Debug.Assert(origPathBytes.Length > 0);
                var newPathBytes = Encoding.UTF8.GetBytes(newPath + "\0");
                if (origPathBytes.Length > maxPathBytes)
                {
                    Console.WriteLine($"Original path is too long");
                    return 1;
                }
                if (newPathBytes.Length > maxPathBytes)
                {
                    Console.WriteLine($"New path is too long");
                    return 1;
                }

                var apphostExeBytes = File.ReadAllBytes(apphostExe);

                // Idempotent: already pointing at the desired relative path.
                if (GetOffset(apphostExeBytes, newPathBytes, requirePathStart: true) >= 0)
                {
                    Console.WriteLine($"Already patched: '{newPath}'");
                    return 0;
                }

                // Prefer an exact bare-dll match (fresh apphost). Do not match the dll name
                // as a suffix of bin\...\dll — that stacked bin\ on every build.ps1 run.
                int offset = GetOffset(apphostExeBytes, origPathBytes, requirePathStart: true);
                if (offset < 0)
                {
                    // Recover from a previously stacked path (bin\bin\...\dll).
                    offset = FindEmbeddedDllPathStart(apphostExeBytes, origPath);
                    if (offset < 0)
                    {
                        Console.WriteLine($"Could not find original path '{origPath}'");
                        return 1;
                    }
                }
                if (offset + newPathBytes.Length > apphostExeBytes.Length)
                {
                    Console.WriteLine($"New path is too long: {newPath}");
                    return 1;
                }
                // Zero the whole path slot so leftover bytes from a longer previous path
                // (e.g. bin\bin\bin\foo.dll) cannot remain after a shorter rewrite.
                for (int i = offset; i < Math.Min(offset + maxPathBytes, apphostExeBytes.Length); i++)
                    apphostExeBytes[i] = 0;
                for (int i = 0; i < newPathBytes.Length; i++)
                    apphostExeBytes[offset + i] = newPathBytes[i];
                File.WriteAllBytes(apphostExe, apphostExeBytes);
                Console.WriteLine($"Patched '{apphostExe}': -> '{newPath}'");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }

        static int GetOffset(byte[] bytes, byte[] pattern, bool requirePathStart)
        {
            int si = 0;
            var b = pattern[0];
            while (si < bytes.Length)
            {
                si = Array.IndexOf(bytes, b, si);
                if (si < 0)
                    break;
                if (Match(bytes, si, pattern))
                {
                    if (!requirePathStart || IsPathStart(bytes, si))
                        return si;
                }
                si++;
            }
            return -1;
        }

        static bool IsPathStart(byte[] bytes, int index)
        {
            if (index <= 0)
                return true;
            var prev = bytes[index - 1];
            // Reject suffix matches inside an already-patched path
            // (e.g. "AnimeStudio.CLI.dll" inside "bin\AnimeStudio.CLI.dll").
            return prev != (byte)'\\' && prev != (byte)'/';
        }

        /// <summary>
        /// Find dllFileName\0 that may sit after one or more "subdir\" prefixes, and return
        /// the start of the full relative path (first subdir).
        /// </summary>
        static int FindEmbeddedDllPathStart(byte[] bytes, string dllFileName)
        {
            var dllBytes = Encoding.UTF8.GetBytes(dllFileName);
            int si = 0;
            while (si < bytes.Length)
            {
                si = Array.IndexOf(bytes, dllBytes[0], si);
                if (si < 0)
                    break;
                if (si + dllBytes.Length < bytes.Length
                    && Match(bytes, si, dllBytes)
                    && bytes[si + dllBytes.Length] == 0)
                {
                    // Walk back over path segments separated by \ or /
                    int start = si;
                    while (start > 0 && (bytes[start - 1] == (byte)'\\' || bytes[start - 1] == (byte)'/'))
                    {
                        int sep = start - 1;
                        int segEnd = sep;
                        int segStart = segEnd;
                        while (segStart > 0)
                        {
                            var c = bytes[segStart - 1];
                            if (c == 0 || c == (byte)'\\' || c == (byte)'/')
                                break;
                            if (c < 32 || c > 126)
                                break;
                            segStart--;
                        }
                        if (segStart == segEnd)
                            break;
                        start = segStart;
                    }
                    return start;
                }
                si++;
            }
            return -1;
        }

        static bool Match(byte[] bytes, int index, byte[] pattern)
        {
            if (index + pattern.Length > bytes.Length)
                return false;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (bytes[index + i] != pattern[i])
                    return false;
            }
            return true;
        }
    }
}
