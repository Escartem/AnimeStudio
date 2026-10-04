using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AnimeStudio.App
{
    // Collects ZZZ resource paths, VO names and audio events found in raw MonoBehaviour strings.
    public sealed class MonoStringScraper
    {
        private static readonly Regex folderRegex = new(@"(?:Assets|UI|IconRole|Data|Scenes|OriginalResRepos|Comic|Weapon)(?:/[^\s"",]+)*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex fileRegex = new(@"(?:Assets|UI|IconRole|Data|Scenes|OriginalResRepos|Comic|Weapon)/[^\s"",]+?\.(?:.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex voRegex = new(@"(?:VO|Breath|Tips)_[^""\s;]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex eventRegex = new(@"(?:Ev|Play|Stop|StateGroup|State|VO|SFX)_[a-zA-Z0-9/_-\{\}]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly string[] keys = { "Assets", "UI", "IconRole", "Data", "Scenes", "State_", "VO_", "Play_", "Stop_", "SFX_" };

        private readonly List<string> paths = new();
        private readonly List<string> voStrings = new();
        private readonly List<string> eventStrings = new();

        public void Scrape(MonoBehaviour m_MonoBehaviour)
        {
            var cleaned = StripIsolatedNulls(m_MonoBehaviour.GetRawData());
            var idx = Search(cleaned, 0);
            while (idx != -1)
            {
                try
                {
                    int len = BinaryPrimitives.ReadInt32LittleEndian(cleaned.AsSpan(idx - 4));
                    string str = Encoding.UTF8.GetString(cleaned.AsSpan(idx, len));

                    foreach (Match match in folderRegex.Matches(str))
                        paths.Add(match.Value.Trim());

                    foreach (Match match in fileRegex.Matches(str))
                    {
                        string subMatch = match.Value.Trim();
                        if (subMatch.StartsWith("UI") || subMatch.StartsWith("Data"))
                            subMatch = $"Assets/NapResources/{subMatch}";
                        else if (subMatch.StartsWith("IconRole"))
                            subMatch = $"Assets/NapResources/UI/Sprite/A1DynamicLoad/{subMatch}";
                        paths.Add(subMatch);
                    }

                    foreach (Match match in voRegex.Matches(str))
                        voStrings.Add(match.Value.Trim());
                    foreach (Match match in eventRegex.Matches(str))
                        eventStrings.Add(match.Value.Trim());
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Error processing MonoBehaviour segment: {ex.Message}");
                }

                idx = Search(cleaned, idx + 4);
            }
        }

        public void Save(string directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(directory, "PathStrings_Sorted.txt"), paths.Distinct().OrderBy(p => p));
            File.WriteAllLines(Path.Combine(directory, "VOStrings_Sorted.txt"), voStrings.Distinct().OrderBy(p => p));
            File.WriteAllLines(Path.Combine(directory, "EventStrings_Sorted.txt"), eventStrings.Distinct().OrderBy(p => p));
        }

        // Single zero bytes are alignment padding between strings, runs of zeros are kept as length prefixes.
        private static byte[] StripIsolatedNulls(byte[] s)
        {
            var cleaned = new List<byte>(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != 0x00
                    || (i > 0 && s[i - 1] == 0x00)
                    || (i < s.Length - 1 && s[i + 1] == 0x00))
                {
                    cleaned.Add(s[i]);
                }
            }
            return cleaned.ToArray();
        }

        private static int Search(byte[] bytes, int startIndex)
        {
            foreach (var key in keys)
            {
                int idx = bytes.Search(key, startIndex);
                if (idx != -1) return idx;
            }
            return -1;
        }
    }
}
