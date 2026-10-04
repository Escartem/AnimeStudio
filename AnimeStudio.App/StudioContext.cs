using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AnimeStudio.App
{
    public sealed class StudioContext
    {
        public const string Z3AssetIndexFile = "Z3-AssetIndex-Eleiyas.json";

        public AssetsManager AssetsManager { get; }
        public AssemblyLoader AssemblyLoader { get; } = new AssemblyLoader();
        public Game Game { get; set; }
        public Dictionary<ulong, string> Z3Paths { get; private set; } = new Dictionary<ulong, string>();

        // Asked once per load when a MonoBehaviour needs its assemblies. Null means never ask.
        public Func<string> RequestAssemblyFolder { get; set; }

        public StudioContext(AssetsManager assetsManager = null)
        {
            AssetsManager = assetsManager ?? new AssetsManager();
        }

        // See https://github.com/Eleiyas/Z3-Asset-Map
        public void LoadZ3Paths()
        {
            var path = Path.Combine(AssetsHelper.MapsDirectory, Z3AssetIndexFile);
            Z3Paths = File.Exists(path)
                ? JsonConvert.DeserializeObject<Dictionary<ulong, string>>(File.ReadAllText(path))
                : new Dictionary<ulong, string>();
            AssetsHelper.Paths = Z3Paths;
        }

        public TypeTree MonoBehaviourToTypeTree(MonoBehaviour m_MonoBehaviour)
        {
            lock (AssemblyLoader)
            {
                if (!AssemblyLoader.Loaded && RequestAssemblyFolder != null)
                {
                    var folder = RequestAssemblyFolder();
                    if (folder != null)
                        AssemblyLoader.Load(folder);
                    else
                        AssemblyLoader.Loaded = true;
                }
            }
            return m_MonoBehaviour.ConvertToTypeTree(AssemblyLoader);
        }

        public void ApplyUnityCNKey(string customKey = null)
        {
            if (Game is UnityCNGame unityCNGame)
            {
                if (Game.Type == GameType.UnityCNCustomKey && customKey != null)
                    UnityCNManager.SetKey(new("UnityCN Custom Key", customKey));
                else
                    UnityCNManager.SetKey(unityCNGame.Key);
            }
        }
    }
}
