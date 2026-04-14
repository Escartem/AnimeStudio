using System;
using System.Collections.Generic;

namespace AnimeStudio
{
    public static class TessellationMeshResolver
    {
        public static Mesh ResolveOriginalMesh(SkinnedMeshRenderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            if (renderer.m_Mesh != null)
            {
                if (!renderer.m_Mesh.IsNull)
                {
                    Logger.Info("TessellationMeshResolver: direct mesh exists, skip SMTD fallback");
                    return null;
                }
            }

            if (renderer.m_GameObject == null ||
                !renderer.m_GameObject.TryGet(out var goObj) ||
                goObj is not GameObject gameObject)
            {
                Logger.Info("TessellationMeshResolver: renderer has no GameObject");
                return null;
            }

            if (!gameObject.HasSkinnedMeshTessellationDataHolder())
            {
                Logger.Info($"TessellationMeshResolver: GameObject={gameObject.Name} has no SMTD holder");
                return null;
            }

            var assetsFile = gameObject.assetsFile;
            if (assetsFile == null)
            {
                Logger.Info($"TessellationMeshResolver: GameObject={gameObject.Name} has no assetsFile");
                return null;
            }

            var assetsManager = assetsFile.assetsManager;
            if (assetsManager == null || assetsManager.assetsFileList == null)
            {
                Logger.Info($"TessellationMeshResolver: GameObject={gameObject.Name} has no assetsManager");
                return null;
            }

            var partName = NormalizePartName(gameObject.Name);
            if (string.IsNullOrEmpty(partName))
            {
                Logger.Info($"TessellationMeshResolver: unable to normalize part name for GameObject={gameObject.Name}");
                return null;
            }

            var candidateNames = BuildCandidateNames(gameObject, partName);

            Logger.Info($"TessellationMeshResolver: resolving mesh for GameObject={gameObject.Name}");
            Logger.Info($"TessellationMeshResolver: candidate names = {string.Join(", ", candidateNames)}");

            // Search across all loaded serialized files, not only the current one.
            foreach (var smtd in EnumerateSkinnedMeshTessellationData(assetsManager))
            {
                if (smtd == null || string.IsNullOrEmpty(smtd.m_Name))
                {
                    continue;
                }

                if (!candidateNames.Contains(smtd.m_Name))
                {
                    continue;
                }

                Logger.Info($"TessellationMeshResolver: matched SMTD {smtd.m_Name}");

                if (smtd.m_OriginalMesh != null &&
                    smtd.m_OriginalMesh.TryGet(out var meshObj) &&
                    meshObj is Mesh mesh)
                {
                    Logger.Info($"TessellationMeshResolver: resolved original mesh from {smtd.m_Name}");
                    return mesh;
                }

                Logger.Info($"TessellationMeshResolver: SMTD {smtd.m_Name} has no resolvable m_OriginalMesh");
            }

            Logger.Info($"TessellationMeshResolver: no matching SMTD found for GameObject={gameObject.Name}");
            return null;
        }

        public static List<SkinnedMeshTessellationData> FindCandidates(SkinnedMeshRenderer renderer)
        {
            var result = new List<SkinnedMeshTessellationData>();

            if (renderer == null)
            {
                return result;
            }

            if (renderer.m_Mesh != null && !renderer.m_Mesh.IsNull)
            {
                return result;
            }

            if (renderer.m_GameObject == null ||
                !renderer.m_GameObject.TryGet(out var goObj) ||
                goObj is not GameObject gameObject)
            {
                return result;
            }

            if (!gameObject.HasSkinnedMeshTessellationDataHolder())
            {
                return result;
            }

            var assetsFile = gameObject.assetsFile;
            if (assetsFile == null)
            {
                return result;
            }

            var assetsManager = assetsFile.assetsManager;
            if (assetsManager == null || assetsManager.assetsFileList == null)
            {
                return result;
            }

            var partName = NormalizePartName(gameObject.Name);
            if (string.IsNullOrEmpty(partName))
            {
                return result;
            }

            var candidateNames = BuildCandidateNames(gameObject, partName);

            foreach (var smtd in EnumerateSkinnedMeshTessellationData(assetsManager))
            {
                if (smtd == null || string.IsNullOrEmpty(smtd.m_Name))
                {
                    continue;
                }

                if (candidateNames.Contains(smtd.m_Name))
                {
                    result.Add(smtd);
                }
            }

            return result;
        }

        private static HashSet<string> BuildCandidateNames(GameObject gameObject, string partName)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rootName in CollectAncestorNames(gameObject))
            {
                result.Add(rootName + "_Model_" + partName);
            }

            return result;
        }

        private static List<string> CollectAncestorNames(GameObject gameObject)
        {
            var result = new List<string>();
            GameObject current = gameObject;

            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Name) && !result.Contains(current.Name))
                {
                    result.Add(current.Name);
                }

                if (current.m_Transform == null ||
                    current.m_Transform.m_Father == null ||
                    !current.m_Transform.m_Father.TryGet(out var parentTransformObj) ||
                    parentTransformObj is not Transform parentTransform ||
                    parentTransform.m_GameObject == null ||
                    !parentTransform.m_GameObject.TryGet(out var parentGameObjectObj) ||
                    parentGameObjectObj is not GameObject parentGameObject)
                {
                    break;
                }

                current = parentGameObject;
            }

            return result;
        }

        private static string NormalizePartName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (name.Equals("Face", StringComparison.OrdinalIgnoreCase))
            {
                return "Face";
            }

            if (name.Equals("Hair", StringComparison.OrdinalIgnoreCase))
            {
                return "Hair";
            }

            if (name.Equals("Body", StringComparison.OrdinalIgnoreCase))
            {
                return "Body";
            }

            return name;
        }

        private static IEnumerable<SkinnedMeshTessellationData> EnumerateSkinnedMeshTessellationData(AssetsManager assetsManager)
        {
            if (assetsManager == null || assetsManager.assetsFileList == null)
            {
                yield break;
            }

            foreach (var serializedFile in assetsManager.assetsFileList)
            {
                if (serializedFile == null || serializedFile.Objects == null)
                {
                    continue;
                }

                foreach (var obj in serializedFile.Objects)
                {
                    if (obj is SkinnedMeshTessellationData smtd)
                    {
                        yield return smtd;
                    }
                }
            }
        }
    }
}