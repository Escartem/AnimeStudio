using System.Collections.Generic;
using System.IO;

namespace AnimeStudio.App
{
    // Resolves hashed containers: ZZZ through the Z3 asset index, Genshin through the loaded AssetIndex.
    public static class ContainerResolver
    {
        public static int Update(IReadOnlyList<AssetRow> rows, Game game, Dictionary<ulong, string> z3Paths)
        {
            if (rows.Count == 0)
                return 0;

            Logger.Info("Updating Containers...");
            var updated = 0;
            var isZZZ = game.Type.IsZZZ();
            foreach (var asset in rows)
            {
                if (isZZZ && ulong.TryParse(asset.Container, out var hash) && z3Paths.TryGetValue(hash, out var z3Path))
                {
                    asset.Container = z3Path;
                    updated++;
                    continue;
                }
                if (asset.SourceFile == null || !int.TryParse(asset.Container, out var value))
                    continue;

                var name = Path.GetFileNameWithoutExtension(asset.SourceFile.originalPath);
                if (!uint.TryParse(name, out var id))
                    continue;

                var path = ResourceIndex.GetContainer(id, unchecked((uint)value));
                if (string.IsNullOrEmpty(path))
                    continue;

                asset.Container = path;
                if (asset.Type == ClassIDType.MiHoYoBinData)
                    asset.Name = Path.GetFileNameWithoutExtension(path);
                updated++;
            }
            Logger.Info("Updated !!");
            return updated;
        }
    }
}
