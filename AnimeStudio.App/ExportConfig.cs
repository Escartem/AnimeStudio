using System.Collections.Generic;

namespace AnimeStudio.App
{
    public sealed class ExportConfig
    {
        public AssetGroupOption GroupOption { get; init; }
        public bool OpenAfterExport { get; init; }

        public bool ConvertTexture { get; init; } = true;
        public bool EnableHDR { get; init; }
        public ImageFormat ImageFormat { get; init; } = ImageFormat.Png;
        public bool ConvertAudio { get; init; } = true;
        public bool RestoreExtensionName { get; init; } = true;
        public bool AllowDuplicates { get; init; }
        public bool ScrapeMonos { get; init; }

        public bool CollectAnimations { get; init; } = true;
        public bool ExportMaterials { get; init; }
        public Dictionary<string, (bool, int)> Uvs { get; init; } = new();
        public Dictionary<string, int> Texs { get; init; } = new();

        public bool EulerFilter { get; init; } = true;
        public decimal FilterPrecision { get; init; } = 0.25m;
        public bool ExportAllNodes { get; init; } = true;
        public bool ExportSkins { get; init; } = true;
        public bool ExportAnimations { get; init; } = true;
        public bool ExportBlendShape { get; init; } = true;
        public bool CastToBone { get; init; }
        public decimal BoneSize { get; init; } = 10;
        public decimal ScaleFactor { get; init; } = 1;
        public int FbxVersion { get; init; } = 3;
        public int FbxFormat { get; init; }

        public ModelConverter.Options CreateModelOptions(Game game, bool exportMaterials)
        {
            return new ModelConverter.Options()
            {
                imageFormat = ImageFormat,
                game = game,
                collectAnimations = CollectAnimations,
                exportMaterials = exportMaterials,
                materials = new HashSet<Material>(),
                uvs = Uvs,
                texs = Texs,
            };
        }

        public Fbx.ExportOptions CreateFbxOptions()
        {
            return new Fbx.ExportOptions()
            {
                eulerFilter = EulerFilter,
                filterPrecision = (float)FilterPrecision,
                exportAllNodes = ExportAllNodes,
                exportSkins = ExportSkins,
                exportAnimations = ExportAnimations,
                exportBlendShape = ExportBlendShape,
                castToBone = CastToBone,
                boneSize = (int)BoneSize,
                scaleFactor = (float)ScaleFactor,
                fbxVersion = FbxVersion,
                fbxFormat = FbxFormat
            };
        }
    }
}
