using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AnimeStudio.App
{
    public sealed class AssetExporter
    {
        private readonly StudioContext context;
        private readonly ExportConfig config;

        public MonoStringScraper Scraper { get; }

        public AssetExporter(StudioContext context, ExportConfig config, MonoStringScraper scraper = null)
        {
            this.context = context;
            this.config = config;
            Scraper = scraper ?? (config.ScrapeMonos ? new MonoStringScraper() : null);
        }

        public bool Export(AssetRow item, string exportPath, ExportType exportType)
        {
            if (item.IsVirtual)
                return ExportVirtual(item, exportPath, exportType);

            return exportType switch
            {
                ExportType.Convert => ExportConvertFile(item, exportPath),
                ExportType.Raw => ExportRawFile(item, exportPath),
                ExportType.Dump => ExportDumpFile(item, exportPath),
                ExportType.JSON => ExportJSONFile(item, exportPath),
                _ => false,
            };
        }

        public bool ExportConvertFile(AssetRow item, string exportPath)
        {
            switch (item.Type)
            {
                case ClassIDType.GameObject:
                    return ExportGameObject(item, exportPath);
                case ClassIDType.Texture2D:
                    return ExportTexture2D(item, exportPath);
                case ClassIDType.AudioClip:
                    return ExportAudioClip(item, exportPath);
                case ClassIDType.Shader:
                    return ExportShader(item, exportPath);
                case ClassIDType.TextAsset:
                    return ExportTextAsset(item, exportPath);
                case ClassIDType.MonoBehaviour:
                    return ExportMonoBehaviour(item, exportPath);
                case ClassIDType.Font:
                    return ExportFont(item, exportPath);
                case ClassIDType.Mesh:
                    return ExportMesh(item, exportPath);
                case ClassIDType.VideoClip:
                    return ExportVideoClip(item, exportPath);
                case ClassIDType.MovieTexture:
                    return ExportMovieTexture(item, exportPath);
                case ClassIDType.Sprite:
                    return ExportSprite(item, exportPath);
                case ClassIDType.Animator:
                    return ExportAnimator(item, exportPath);
                case ClassIDType.AnimationClip:
                    return ExportAnimationClip(item, exportPath);
                case ClassIDType.MiHoYoBinData:
                    return ExportMiHoYoBinData(item, exportPath);
                case ClassIDType.Material:
                case ClassIDType.NapAssetBundleIndexAsset:
                    return ExportJSONFile(item, exportPath);
                default:
                    return ExportRawFile(item, exportPath);
            }
        }

        #region Virtual (loose AFK Journey files)
        private bool ExportVirtual(AssetRow item, string exportPath, ExportType exportType)
        {
            switch (exportType)
            {
                case ExportType.Convert:
                    switch (item.Type)
                    {
                        case ClassIDType.Texture2D:
                            return AFKJourneyUtils.DecodeDxtFile(item.ExternalPath, Path.Combine(exportPath, item.Name)) != null;
                        case ClassIDType.TextAsset:
                            return ExportVirtualText(item, exportPath);
                        default:
                            return false;
                    }
                case ExportType.Raw:
                    var extension = Path.GetExtension(item.ExternalPath);
                    if (!TryExportFile(exportPath, item, string.IsNullOrEmpty(extension) ? ".dat" : extension, out var rawPath))
                        return false;
                    File.Copy(item.ExternalPath, rawPath, overwrite: false);
                    return true;
                case ExportType.JSON when item.Type == ClassIDType.TextAsset:
                    return ExportVirtualText(item, exportPath);
                case ExportType.Dump:
                case ExportType.JSON:
                    if (!TryExportFile(exportPath, item, ".txt", out var dumpPath))
                        return false;
                    File.WriteAllText(dumpPath, item.VirtualContent ?? item.InfoText ?? item.ExternalPath);
                    return true;
                default:
                    return false;
            }
        }

        private bool ExportVirtualText(AssetRow item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".json", out var textPath))
                return false;
            var text = item.VirtualContent ?? AFKJourneyUtils.DecryptJsoneToText(File.ReadAllBytes(item.ExternalPath));
            File.WriteAllText(textPath, text);
            return true;
        }
        #endregion

        public bool ExportTexture2D(AssetRow item, string exportPath)
        {
            var m_Texture2D = (Texture2D)item.Asset;
            if (!config.ConvertTexture)
            {
                if (!TryExportFile(exportPath, item, ".tex", out var rawPath))
                    return false;
                File.WriteAllBytes(rawPath, m_Texture2D.image_data.GetData());
                return true;
            }

            var isHDR = m_Texture2D.m_TextureFormat == TextureFormat.RGBAHalf && config.EnableHDR;
            var type = isHDR ? ImageFormat.Hdr : config.ImageFormat;
            if (!TryExportFile(exportPath, item, "." + type.ToString().ToLower(), out var exportFullPath))
                return false;

            if (isHDR)
            {
                WriteRadianceHdr(m_Texture2D, exportFullPath);
                return true;
            }

            var image = m_Texture2D.ConvertToImage(true);
            if (image == null)
                return false;
            using (image)
            using (var file = File.Create(exportFullPath))
            {
                image.WriteToStream(file, type);
            }
            return true;
        }

        private static void WriteRadianceHdr(Texture2D m_Texture2D, string path)
        {
            var width = m_Texture2D.m_Width;
            var height = m_Texture2D.m_Height;
            var rowSize = width * 4;
            var source = ArrayPool<byte>.Shared.Rent((int)m_Texture2D.image_data.Size);
            var row = ArrayPool<byte>.Shared.Rent(rowSize);
            try
            {
                m_Texture2D.image_data.GetData(source);

                using var file = new BufferedStream(File.Create(path), 1 << 16);
                var header = $"#?RADIANCE\nPRIMARIES=0 0 0 0 0 0 0 0\nFORMAT=32-bit_rle_rgbe\n\n-Y {height} +X {width}\n";
                file.Write(Encoding.ASCII.GetBytes(header));

                // Rows are written bottom-up to flip the image. Encoding from https://github.com/Opioid/rgbe/blob/master/encode.go
                for (var y = height - 1; y >= 0; y--)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var src = (y * width + x) * 8;
                        var r = (float)BitConverter.ToHalf(source, src);
                        var g = (float)BitConverter.ToHalf(source, src + 2);
                        var b = (float)BitConverter.ToHalf(source, src + 4);
                        var max = Math.Max(r, Math.Max(g, b));
                        var dst = x * 4;
                        if (max < 1e-32f)
                        {
                            row[dst] = row[dst + 1] = row[dst + 2] = row[dst + 3] = 0;
                            continue;
                        }
                        var mantissa = (float)Double.Frexp(max, out var exponent) * 256f / max;
                        row[dst] = (byte)(r * mantissa);
                        row[dst + 1] = (byte)(g * mantissa);
                        row[dst + 2] = (byte)(b * mantissa);
                        row[dst + 3] = (byte)(exponent + 128);
                    }
                    file.Write(row, 0, rowSize);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(source);
                ArrayPool<byte>.Shared.Return(row);
            }
        }

        public bool ExportAudioClip(AssetRow item, string exportPath)
        {
            var m_AudioClip = (AudioClip)item.Asset;
            var m_AudioData = m_AudioClip.m_AudioData.GetData();
            if (m_AudioData == null || m_AudioData.Length == 0)
                return false;
            var converter = new AudioClipConverter(m_AudioClip);
            if (config.ConvertAudio && converter.IsSupport)
            {
                if (!TryExportFile(exportPath, item, ".wav", out var exportFullPath))
                    return false;
                var buffer = converter.ConvertToWav();
                if (buffer == null)
                    return false;
                File.WriteAllBytes(exportFullPath, buffer);
            }
            else
            {
                if (!TryExportFile(exportPath, item, converter.GetExtensionName(), out var exportFullPath))
                    return false;
                File.WriteAllBytes(exportFullPath, m_AudioData);
            }
            return true;
        }

        public bool ExportShader(AssetRow item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".shader", out var exportFullPath))
                return false;
            File.WriteAllText(exportFullPath, ((Shader)item.Asset).Convert());
            return true;
        }

        public bool ExportTextAsset(AssetRow item, string exportPath)
        {
            var m_TextAsset = (TextAsset)item.Asset;
            if (!TryExportFile(exportPath, item, RestoredExtension(item, ".txt"), out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, m_TextAsset.m_Script);
            return true;
        }

        public bool ExportMonoBehaviour(AssetRow item, string exportPath)
        {
            var m_MonoBehaviour = (MonoBehaviour)item.Asset;
            if (Scraper != null)
            {
                Scraper.Scrape(m_MonoBehaviour);
                return true;
            }

            if (!TryExportFile(exportPath, item, ".json", out var exportFullPath))
                return false;
            var type = m_MonoBehaviour.ToType() ?? m_MonoBehaviour.ToType(context.MonoBehaviourToTypeTree(m_MonoBehaviour));
            File.WriteAllText(exportFullPath, JsonConvert.SerializeObject(type, Formatting.Indented));
            return true;
        }

        public bool ExportMiHoYoBinData(AssetRow item, string exportPath)
        {
            if (item.Asset is not MiHoYoBinData m_MiHoYoBinData)
                return false;

            string exportFullPath;
            switch (m_MiHoYoBinData.Type)
            {
                case MiHoYoBinDataType.JSON:
                    if (!TryExportFile(exportPath, item, ".json", out exportFullPath))
                        return false;
                    var json = m_MiHoYoBinData.Dump() as string;
                    if (json.Length != 0)
                    {
                        File.WriteAllText(exportFullPath, json);
                        return true;
                    }
                    break;
                case MiHoYoBinDataType.Bytes:
                    if (!TryExportFile(exportPath, item, RestoredExtension(item, ".bin"), out exportFullPath))
                        return false;
                    var bytes = m_MiHoYoBinData.Dump() as byte[];
                    if (!bytes.IsNullOrEmpty())
                    {
                        File.WriteAllBytes(exportFullPath, bytes);
                        return true;
                    }
                    break;
            }
            return false;
        }

        public bool ExportFont(AssetRow item, string exportPath)
        {
            var m_Font = (Font)item.Asset;
            if (m_Font.m_FontData == null)
                return false;

            var extension = m_Font.m_FontData.AsSpan().StartsWith("OTTO"u8) ? ".otf" : ".ttf";
            if (!TryExportFile(exportPath, item, extension, out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, m_Font.m_FontData);
            return true;
        }

        public bool ExportMesh(AssetRow item, string exportPath)
        {
            var m_Mesh = (Mesh)item.Asset;
            if (m_Mesh.m_VertexCount <= 0 || m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0)
                return false;
            if (!TryExportFile(exportPath, item, ".obj", out var exportFullPath))
                return false;

            var sb = new StringBuilder();
            sb.AppendLine("g " + m_Mesh.m_Name);

            int c = m_Mesh.m_Vertices.Length == m_Mesh.m_VertexCount * 4 ? 4 : 3;
            for (int v = 0; v < m_Mesh.m_VertexCount; v++)
            {
                sb.AppendFormat("v {0} {1} {2}\r\n", -m_Mesh.m_Vertices[v * c], m_Mesh.m_Vertices[v * c + 1], m_Mesh.m_Vertices[v * c + 2]);
            }

            if (m_Mesh.m_UV0?.Length > 0)
            {
                c = m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 2 ? 2 : m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 3 ? 3 : 4;
                for (int v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    sb.AppendFormat("vt {0} {1}\r\n", m_Mesh.m_UV0[v * c], m_Mesh.m_UV0[v * c + 1]);
                }
            }

            if (m_Mesh.m_Normals?.Length > 0)
            {
                if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 3)
                    c = 3;
                else if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 4)
                    c = 4;
                for (int v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    sb.AppendFormat("vn {0} {1} {2}\r\n", -m_Mesh.m_Normals[v * c], m_Mesh.m_Normals[v * c + 1], m_Mesh.m_Normals[v * c + 2]);
                }
            }

            int sum = 0;
            for (var i = 0; i < m_Mesh.m_SubMeshes.Count; i++)
            {
                sb.AppendLine($"g {m_Mesh.m_Name}_{i}");
                int indexCount = (int)m_Mesh.m_SubMeshes[i].indexCount;
                var end = sum + indexCount / 3;
                for (int f = sum; f < end; f++)
                {
                    sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\r\n", m_Mesh.m_Indices[f * 3 + 2] + 1, m_Mesh.m_Indices[f * 3 + 1] + 1, m_Mesh.m_Indices[f * 3] + 1);
                }
                sum = end;
            }

            sb.Replace("NaN", "0");
            File.WriteAllText(exportFullPath, sb.ToString());
            return true;
        }

        public bool ExportVideoClip(AssetRow item, string exportPath)
        {
            var m_VideoClip = (VideoClip)item.Asset;
            if (m_VideoClip.m_ExternalResources.m_Size <= 0)
                return false;
            if (!TryExportFile(exportPath, item, Path.GetExtension(m_VideoClip.m_OriginalPath), out var exportFullPath))
                return false;
            m_VideoClip.m_VideoData.WriteData(exportFullPath);
            return true;
        }

        public bool ExportMovieTexture(AssetRow item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".ogv", out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, ((MovieTexture)item.Asset).m_MovieData);
            return true;
        }

        public bool ExportSprite(AssetRow item, string exportPath)
        {
            var type = config.ImageFormat;
            if (!TryExportFile(exportPath, item, "." + type.ToString().ToLower(), out var exportFullPath))
                return false;
            var image = ((Sprite)item.Asset).GetImage();
            if (image == null)
                return false;
            using (image)
            using (var file = File.Create(exportFullPath))
            {
                image.WriteToStream(file, type);
            }
            return true;
        }

        public bool ExportRawFile(AssetRow item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".dat", out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, item.Asset.GetRawData());
            return true;
        }

        public bool ExportAnimationClip(AssetRow item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".anim", out var exportFullPath))
                return false;
            var str = ((AnimationClip)item.Asset).Convert();
            if (string.IsNullOrEmpty(str))
                return false;
            File.WriteAllText(exportFullPath, str);
            return true;
        }

        public bool ExportAnimator(AssetRow item, string exportPath, IReadOnlyList<AssetRow> animationList = null)
        {
            if (!TryExportFolder(exportPath, item, out var exportFullPath))
                return false;

            exportFullPath = Path.Combine(exportFullPath, item.Name + ".fbx");
            var options = config.CreateModelOptions(context.Game, config.ExportMaterials);
            var clips = ToClips(animationList);
            var convert = clips != null
                ? new ModelConverter((Animator)item.Asset, options, clips)
                : new ModelConverter((Animator)item.Asset, options);
            ExportMaterials(options, Path.GetDirectoryName(exportFullPath));
            ExportFbx(convert, exportFullPath);
            return true;
        }

        public bool ExportGameObject(AssetRow item, string exportPath, IReadOnlyList<AssetRow> animationList = null)
        {
            if (!TryExportFolder(exportPath, item, out var exportFullPath))
                return false;
            return ExportGameObject((GameObject)item.Asset, exportFullPath + Path.DirectorySeparatorChar, animationList);
        }

        public bool ExportGameObject(GameObject gameObject, string exportPath, IReadOnlyList<AssetRow> animationList = null)
        {
            var options = config.CreateModelOptions(context.Game, config.ExportMaterials);
            var clips = ToClips(animationList);
            var convert = clips != null
                ? new ModelConverter(gameObject, options, clips)
                : new ModelConverter(gameObject, options);

            if (convert.MeshList.Count == 0)
            {
                Logger.Info($"GameObject {gameObject.m_Name} has no mesh, skipping...");
                return false;
            }
            ExportMaterials(options, exportPath);
            ExportFbx(convert, exportPath + FixFileName(gameObject.m_Name) + ".fbx");
            return true;
        }

        public void ExportGameObjectMerge(List<GameObject> gameObjects, string exportPath, IReadOnlyList<AssetRow> animationList = null)
        {
            var rootName = Path.GetFileNameWithoutExtension(exportPath);
            var options = config.CreateModelOptions(context.Game, config.ExportMaterials);
            var clips = ToClips(animationList);
            var convert = clips != null
                ? new ModelConverter(rootName, gameObjects, options, clips)
                : new ModelConverter(rootName, gameObjects, options);
            ExportMaterials(options, Path.GetDirectoryName(exportPath));
            ExportFbx(convert, exportPath);
        }

        private static AnimationClip[] ToClips(IReadOnlyList<AssetRow> animationList)
            => animationList?.Select(x => (AnimationClip)x.Asset).ToArray();

        private void ExportMaterials(ModelConverter.Options options, string directory)
        {
            if (!options.exportMaterials)
                return;
            var materialExportPath = Path.Combine(directory, "Materials");
            Directory.CreateDirectory(materialExportPath);
            foreach (var material in options.materials)
            {
                ExportJSONFile(new AssetRow(material), materialExportPath);
            }
        }

        private void ExportFbx(IImported convert, string exportPath)
        {
            ModelExporter.ExportFbx(exportPath, convert, config.CreateFbxOptions());
        }

        public bool ExportDumpFile(AssetRow item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".txt", out var exportFullPath))
                return false;
            var str = item.Asset.Dump();
            if (str == null && item.Asset is MonoBehaviour m_MonoBehaviour)
            {
                str = m_MonoBehaviour.Dump(context.MonoBehaviourToTypeTree(m_MonoBehaviour));
            }
            if (str == null)
                return false;
            File.WriteAllText(exportFullPath, str);
            return true;
        }

        public bool ExportJSONFile(AssetRow item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".json", out var exportFullPath))
                return false;
            File.WriteAllText(exportFullPath, SerializeJson(item.Asset));
            return true;
        }

        public static string SerializeJson(object obj)
        {
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new StringEnumConverter());
            return JsonConvert.SerializeObject(obj, Formatting.Indented, settings);
        }

        private string RestoredExtension(AssetRow item, string fallback)
        {
            if (config.RestoreExtensionName && !string.IsNullOrEmpty(item.Container))
                return Path.GetExtension(item.Container);
            return fallback;
        }

        private bool TryExportFile(string dir, AssetRow item, string extension, out string fullPath)
        {
            var fileName = FixFileName(item.Name);
            fullPath = Path.Combine(dir, $"{fileName}{extension}");
            if (!File.Exists(fullPath))
            {
                Directory.CreateDirectory(dir);
                return true;
            }
            if (config.AllowDuplicates)
            {
                for (int i = 1; i < int.MaxValue; i++)
                {
                    fullPath = Path.Combine(dir, $"{fileName} ({i}){extension}");
                    if (!File.Exists(fullPath))
                        return true;
                }
            }
            return false;
        }

        private bool TryExportFolder(string dir, AssetRow item, out string fullPath)
        {
            var fileName = FixFileName(item.Name);
            fullPath = Path.Combine(dir, fileName);
            if (!Directory.Exists(fullPath))
                return true;
            if (config.AllowDuplicates)
            {
                for (int i = 1; i < int.MaxValue; i++)
                {
                    fullPath = Path.Combine(dir, $"{fileName} ({i})");
                    if (!Directory.Exists(fullPath))
                        return true;
                }
            }
            return false;
        }

        public static string FixFileName(string str)
        {
            if (str.Length >= 260) return Path.GetRandomFileName();
            return Path.GetInvalidFileNameChars().Aggregate(str, (current, c) => current.Replace(c, '_'));
        }
    }
}
