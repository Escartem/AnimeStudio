using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Diagnostics;
using System.Windows.Forms;

namespace AnimeStudio.GUI
{
    internal static partial class Studio
    {
        public static TypeTree MonoBehaviourToTypeTree(MonoBehaviour m_MonoBehaviour)
        {
            if (!assemblyLoader.Loaded)
            {
                var openFolderDialog = new OpenFolderDialog();
                openFolderDialog.Title = "Select Assembly Folder";
                if (openFolderDialog.ShowDialog() == DialogResult.OK)
                {
                    assemblyLoader.Load(openFolderDialog.Folder);
                }
                else
                {
                    assemblyLoader.Loaded = true;
                }
            }
            return m_MonoBehaviour.ConvertToTypeTree(assemblyLoader);
        }

        public static string DumpAsset(Object obj)
        {
            var str = obj.Dump();
            if (str == null && obj is MonoBehaviour m_MonoBehaviour)
            {
                var type = MonoBehaviourToTypeTree(m_MonoBehaviour);
                str = m_MonoBehaviour.Dump(type);
            }
            if (string.IsNullOrEmpty(str))
            {
                var settings = new JsonSerializerSettings();
                settings.Converters.Add(new StringEnumConverter());
                str = JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented, settings);
            }
            return str;
        }

        public static void OpenFolderInExplorer(string path)
        {
            var info = new ProcessStartInfo(path);
            info.UseShellExecute = true;
            Process.Start(info);
        }
    }
}

