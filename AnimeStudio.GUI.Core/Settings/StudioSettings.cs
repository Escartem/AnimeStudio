using System.Collections.Generic;
using AnimeStudio.App;
using Newtonsoft.Json;

namespace AnimeStudio.GUI.Core
{
    public enum GuiColorTheme
    {
        System = 0,
        Dark = 1,
        Light = 2
    }

    public sealed class StudioSettings
    {
        private const string DefaultTypesJson = """{"Animation":{"Item1":true,"Item2":false},"AnimationClip":{"Item1":true,"Item2":true},"Animator":{"Item1":true,"Item2":true},"AnimatorController":{"Item1":true,"Item2":false},"AnimatorOverrideController":{"Item1":true,"Item2":false},"AssetBundle":{"Item1":true,"Item2":false},"AudioClip":{"Item1":true,"Item2":true},"Avatar":{"Item1":true,"Item2":false},"Font":{"Item1":true,"Item2":true},"GameObject":{"Item1":true,"Item2":false},"IndexObject":{"Item1":true,"Item2":false},"Material":{"Item1":true,"Item2":true},"Mesh":{"Item1":true,"Item2":true},"MeshFilter":{"Item1":true,"Item2":false},"MeshRenderer":{"Item1":true,"Item2":false},"MiHoYoBinData":{"Item1":true,"Item2":true},"MonoBehaviour":{"Item1":true,"Item2":true},"MonoScript":{"Item1":true,"Item2":false},"MovieTexture":{"Item1":true,"Item2":true},"PlayerSettings":{"Item1":true,"Item2":false},"RectTransform":{"Item1":true,"Item2":false},"Shader":{"Item1":true,"Item2":true},"SkinnedMeshRenderer":{"Item1":true,"Item2":false},"Sprite":{"Item1":true,"Item2":true},"SpriteAtlas":{"Item1":true,"Item2":false},"TextAsset":{"Item1":true,"Item2":true},"Texture2D":{"Item1":true,"Item2":true},"Transform":{"Item1":true,"Item2":false},"VideoClip":{"Item1":true,"Item2":true},"ResourceManager":{"Item1":true,"Item2":false},"NapAssetBundleIndexAsset":{"Item1":true,"Item2":true}}""";
        private const string DefaultUvsJson = """{"UV0":{"Item1":true,"Item2":0},"UV1":{"Item1":true,"Item2":1},"UV2":{"Item1":false,"Item2":0},"UV3":{"Item1":false,"Item2":0},"UV4":{"Item1":false,"Item2":0},"UV5":{"Item1":false,"Item2":0},"UV6":{"Item1":false,"Item2":0},"UV7":{"Item1":false,"Item2":0}}""";

        // View
        public bool DisplayAll { get; set; }
        public bool EnablePreview { get; set; } = true;
        public bool DisplayInfo { get; set; } = true;
        public bool EnableModelPreview { get; set; }
        public bool ModelsOnly { get; set; }
        public GuiColorTheme Theme { get; set; } = GuiColorTheme.System;

        // Debug
        public bool EnableConsole { get; set; } = true;
        public bool EnableFileLogging { get; set; }
        public bool ShowErrorMessages { get; set; } = true;
        public LoggerEvent LoggerEvents { get; set; } = LoggerEvent.Debug | LoggerEvent.Info | LoggerEvent.Warning | LoggerEvent.Error;

        // Loading
        public int SelectedGame { get; set; }
        public string LastUnityCNKey { get; set; } = string.Empty;
        public bool ResolveDependencies { get; set; } = true;
        public bool SkipContainer { get; set; }
        public bool UseBundleContainerName { get; set; }
        public Dictionary<ClassIDType, (bool Parse, bool Export)> Types { get; set; } = DefaultTypes();

        // Export
        public bool OpenAfterExport { get; set; } = true;
        public AssetGroupOption AssetGroupOption { get; set; } = AssetGroupOption.ByType;
        public bool RestoreExtensionName { get; set; } = true;
        public bool AllowDuplicates { get; set; }
        public bool ConvertTexture { get; set; } = true;
        public bool EnableHDR { get; set; }
        public ImageFormat ConvertType { get; set; } = ImageFormat.Png;
        public bool ConvertAudio { get; set; } = true;
        public bool Encrypted { get; set; } = true;
        public byte Key { get; set; } = 147;

        // FBX
        public bool EulerFilter { get; set; } = true;
        public decimal FilterPrecision { get; set; } = 0.25m;
        public bool ExportAllNodes { get; set; } = true;
        public bool ExportSkins { get; set; } = true;
        public bool ExportAnimations { get; set; } = true;
        public bool ExportBlendShape { get; set; } = true;
        public bool CastToBone { get; set; }
        public decimal BoneSize { get; set; } = 10;
        public decimal ScaleFactor { get; set; } = 1;
        public int FbxVersion { get; set; } = 3;
        public int FbxFormat { get; set; }
        public bool CollectAnimations { get; set; } = true;
        public bool ExportMaterials { get; set; }
        public Dictionary<string, (bool Enabled, int Type)> Uvs { get; set; } = DefaultUvs();
        public Dictionary<string, int> Texs { get; set; } = new();

        // Maps
        public bool BuildCabMap { get; set; } = true;
        public ExportListType AssetMapType { get; set; } = ExportListType.MessagePack;
        public bool MinimalAssetMap { get; set; } = true;
        // Map name inside the Maps folder, or a full path for maps living elsewhere.
        public string SelectedCABMap { get; set; } = string.Empty;
        public List<string> RecentCABMaps { get; set; } = new();

        public static Dictionary<ClassIDType, (bool, bool)> DefaultTypes() => JsonConvert.DeserializeObject<Dictionary<ClassIDType, (bool, bool)>>(DefaultTypesJson);
        public static Dictionary<string, (bool, int)> DefaultUvs() => JsonConvert.DeserializeObject<Dictionary<string, (bool, int)>>(DefaultUvsJson);

        public ExportConfig ToExportConfig() => new()
        {
            GroupOption = AssetGroupOption,
            OpenAfterExport = OpenAfterExport,
            ConvertTexture = ConvertTexture,
            EnableHDR = EnableHDR,
            ImageFormat = ConvertType,
            ConvertAudio = ConvertAudio,
            RestoreExtensionName = RestoreExtensionName,
            AllowDuplicates = AllowDuplicates,
            CollectAnimations = CollectAnimations,
            ExportMaterials = ExportMaterials,
            Uvs = new Dictionary<string, (bool, int)>(Uvs),
            Texs = new Dictionary<string, int>(Texs),
            EulerFilter = EulerFilter,
            FilterPrecision = FilterPrecision,
            ExportAllNodes = ExportAllNodes,
            ExportSkins = ExportSkins,
            ExportAnimations = ExportAnimations,
            ExportBlendShape = ExportBlendShape,
            CastToBone = CastToBone,
            BoneSize = BoneSize,
            ScaleFactor = ScaleFactor,
            FbxVersion = FbxVersion,
            FbxFormat = FbxFormat,
        };

        public CatalogOptions ToCatalogOptions() => new()
        {
            DisplayAll = DisplayAll,
            SkipContainer = SkipContainer,
            UseBundleContainerName = UseBundleContainerName,
        };

        // Pushes the settings that the core reads from static state.
        public void ApplyToCore()
        {
            TypeFlags.SetTypes(Types);
            MiHoYoBinData.Encrypted = Encrypted;
            MiHoYoBinData.Key = Key;
            AssetsHelper.Minimal = MinimalAssetMap;
        }
    }
}
