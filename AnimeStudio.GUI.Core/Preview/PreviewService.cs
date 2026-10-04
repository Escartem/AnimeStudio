using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnimeStudio.App;
using Newtonsoft.Json;

namespace AnimeStudio.GUI.Core.Preview
{
    public sealed class PreviewService
    {
        private const string ModelHelp = "'Mouse Left'=Rotate | 'Mouse Right'=Move | 'Mouse Wheel'=Zoom \n'Ctrl W'=Wireframe | 'Ctrl S'=Shade | 'Ctrl N'=ReNormal ";

        private readonly StudioContext context;
        private readonly Func<StudioSettings> settings;
        private readonly PreviewCache cache = new PreviewCache(256L * 1024 * 1024);

        public PreviewService(StudioContext context, Func<StudioSettings> settings)
        {
            this.context = context;
            this.settings = settings;
        }

        public void ClearCache() => cache.Clear();

        public async Task<PreviewResult> GetPreviewAsync(AssetRow row, CancellationToken token)
        {
            if (cache.TryGet(row, out var cached))
                return cached;

            var result = await AssetIo.RunAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    return row.IsVirtual ? CreateVirtual(row) : Create(row);
                }
                catch (Exception e)
                {
                    Logger.Error($"Preview {row.Type}:{row.Name} error\r\n{e.Message}\r\n{e.StackTrace}");
                    return new NoPreview();
                }
            }, token).ConfigureAwait(false);

            if (result is not NoPreview && result is not AudioPreview)
                cache.Add(row, result);
            return result;
        }

        public Task<string> GetDumpAsync(AssetRow row, CancellationToken token)
        {
            if (row.IsVirtual)
                return Task.FromResult(row.VirtualContent ?? row.InfoText ?? row.ExternalPath);
            return AssetIo.RunAsync(() => Dump(row.Asset), token);
        }

        public string Dump(Object obj)
        {
            var str = obj.Dump();
            if (str == null && obj is MonoBehaviour m_MonoBehaviour)
                str = m_MonoBehaviour.Dump(context.MonoBehaviourToTypeTree(m_MonoBehaviour));
            return string.IsNullOrEmpty(str) ? AssetExporter.SerializeJson(obj) : str;
        }

        private PreviewResult Create(AssetRow row)
        {
            var enableModelPreview = settings().EnableModelPreview;
            switch (row.Asset)
            {
                case GameObject m_GameObject when enableModelPreview:
                    return Model(new ModelConverter(m_GameObject, ModelOptions(), Array.Empty<AnimationClip>()));
                case Animator m_Animator when enableModelPreview:
                    return Model(new ModelConverter(m_Animator, ModelOptions(), Array.Empty<AnimationClip>()));
                case Texture2D m_Texture2D:
                    return Texture(m_Texture2D);
                case AudioClip m_AudioClip:
                    return Audio(m_AudioClip);
                case Shader m_Shader:
                    if (m_Shader.byteSize > 0xFFFFFFF)
                        return new TextPreview("Shader is too large to parse");
                    return new TextPreview(m_Shader.Convert() ?? "Serialized Shader can't be read");
                case TextAsset m_TextAsset:
                    return new TextPreview(Encoding.UTF8.GetString(m_TextAsset.m_Script).Replace("\0", ""));
                case MonoBehaviour m_MonoBehaviour:
                    var obj = m_MonoBehaviour.ToType() ?? m_MonoBehaviour.ToType(context.MonoBehaviourToTypeTree(m_MonoBehaviour));
                    return new TextPreview(JsonConvert.SerializeObject(obj, Formatting.Indented));
                case Font m_Font:
                    return m_Font.m_FontData != null
                        ? new FontPreview(m_Font.m_FontData)
                        : new NoPreview { Status = "Unsupported font for preview. Try to export." };
                case Mesh m_Mesh:
                    var mesh = MeshData.FromMesh(m_Mesh);
                    return mesh != null
                        ? new MeshPreview(mesh) { Status = ModelHelp }
                        : new NoPreview { Status = "Unable to preview this mesh" };
                case VideoClip:
                case MovieTexture:
                    return new NoPreview { Status = "Only supported export." };
                case Sprite m_Sprite:
                    return Sprite(m_Sprite);
                case AnimationClip m_AnimationClip:
                    var clip = m_AnimationClip.Convert();
                    return new TextPreview(string.IsNullOrEmpty(clip) ? "Legacy animation is not supported" : clip);
                case MiHoYoBinData m_MiHoYoBinData:
                    return new TextPreview(m_MiHoYoBinData.AsString) { Status = "Can be exported/previewed as JSON if data is a valid JSON (check XOR)." };
                case NapAssetBundleIndexAsset:
                    return new TextPreview(Dump(row.Asset));
                default:
                    var str = row.Asset.Dump();
                    return str != null ? new TextPreview(str) : new NoPreview();
            }
        }

        private ModelConverter.Options ModelOptions() => settings().ToExportConfig().CreateModelOptions(context.Game, exportMaterials: false);

        private static PreviewResult Model(ModelConverter model)
        {
            var mesh = MeshData.FromModel(model);
            return mesh != null
                ? new MeshPreview(mesh) { Status = ModelHelp }
                : new NoPreview { Status = "Unable to preview this model" };
        }

        private static PreviewResult Texture(Texture2D m_Texture2D)
        {
            using var image = m_Texture2D.ConvertToImage(true);
            var pixels = image?.ConvertToBytes();
            if (pixels == null)
                return new NoPreview { Status = "Unsupported image for preview" };

            var info = new StringBuilder();
            info.Append($"Width: {m_Texture2D.m_Width}\nHeight: {m_Texture2D.m_Height}\nFormat: {m_Texture2D.m_TextureFormat}");
            var settings = m_Texture2D.m_TextureSettings;
            info.Append(settings.m_FilterMode switch
            {
                0 => "\nFilter Mode: Point ",
                1 => "\nFilter Mode: Bilinear ",
                2 => "\nFilter Mode: Trilinear ",
                _ => "",
            });
            info.Append($"\nAnisotropic level: {settings.m_Aniso}\nMip map bias: {settings.m_MipBias}");
            info.Append(settings.m_WrapMode switch
            {
                0 => "\nWrap mode: Repeat",
                1 => "\nWrap mode: Clamp",
                _ => "",
            });
            return new TexturePreview(pixels, m_Texture2D.m_Width, m_Texture2D.m_Height, ShowChannels: true)
            {
                InfoText = info.ToString(),
                Status = "'Ctrl'+'R'/'G'/'B'/'A' for Channel Toggle",
            };
        }

        private static PreviewResult Sprite(Sprite m_Sprite)
        {
            using var image = m_Sprite.GetImage();
            var pixels = image?.ConvertToBytes();
            if (pixels == null)
                return new NoPreview { Status = "Unsupported sprite for preview." };
            return new TexturePreview(pixels, image.Width, image.Height, ShowChannels: false)
            {
                InfoText = $"Width: {image.Width}\nHeight: {image.Height}\n",
            };
        }

        private static PreviewResult Audio(AudioClip m_AudioClip)
        {
            var info = "Compression format: " + (m_AudioClip.version[0] < 5
                ? m_AudioClip.m_Type switch
                {
                    FMODSoundType.ACC => "Acc",
                    FMODSoundType.AIFF => "AIFF",
                    FMODSoundType.IT => "Impulse tracker",
                    FMODSoundType.MOD => "Protracker / Fasttracker MOD",
                    FMODSoundType.MPEG => "MP2/MP3 MPEG",
                    FMODSoundType.OGGVORBIS => "Ogg vorbis",
                    FMODSoundType.S3M => "ScreamTracker 3",
                    FMODSoundType.WAV => "Microsoft WAV",
                    FMODSoundType.XM => "FastTracker 2 XM",
                    FMODSoundType.XMA => "Xbox360 XMA",
                    FMODSoundType.VAG => "PlayStation Portable ADPCM",
                    FMODSoundType.AUDIOQUEUE => "iPhone",
                    _ => "Unknown",
                }
                : m_AudioClip.m_CompressionFormat switch
                {
                    AudioCompressionFormat.PCM => "PCM",
                    AudioCompressionFormat.Vorbis => "Vorbis",
                    AudioCompressionFormat.ADPCM => "ADPCM",
                    AudioCompressionFormat.MP3 => "MP3",
                    AudioCompressionFormat.PSMVAG => "PlayStation Portable ADPCM",
                    AudioCompressionFormat.HEVAG => "PSVita ADPCM",
                    AudioCompressionFormat.XMA => "Xbox360 XMA",
                    AudioCompressionFormat.AAC => "AAC",
                    AudioCompressionFormat.GCADPCM => "Nintendo 3DS/Wii DSP",
                    AudioCompressionFormat.ATRAC9 => "PSVita ATRAC9",
                    _ => "Unknown",
                });

            var data = m_AudioClip.m_AudioData.GetData();
            if (data == null || data.Length == 0)
                return new NoPreview { InfoText = info };
            return new AudioPreview(data, (uint)m_AudioClip.m_Size) { InfoText = info };
        }

        private static PreviewResult CreateVirtual(AssetRow row)
        {
            switch (row.Type)
            {
                case ClassIDType.Texture2D:
                    var decoded = AFKJourneyUtils.DecodeDxtToBitmapData(row.ExternalPath);
                    FlipVertically(decoded.Pixels, decoded.Width, decoded.Height);
                    return new TexturePreview(decoded.Pixels, decoded.Width, decoded.Height, ShowChannels: false)
                    {
                        InfoText = $"Width: {decoded.Width}\nHeight: {decoded.Height}\nFormat: {decoded.Format}\nPath: {row.ExternalPath}",
                        Status = "AFK Journey loose DXT texture preview",
                    };
                case ClassIDType.TextAsset:
                    row.VirtualContent ??= AFKJourneyUtils.DecryptJsoneToText(File.ReadAllBytes(row.ExternalPath));
                    return new TextPreview(row.VirtualContent)
                    {
                        InfoText = $"Path: {row.ExternalPath}",
                        Status = "AFK Journey loose JSOne preview",
                    };
                default:
                    return new NoPreview { Status = "Unsupported AFK Journey loose asset preview." };
            }
        }

        private static void FlipVertically(byte[] pixels, int width, int height)
        {
            var stride = width * 4;
            var row = new byte[stride];
            for (var y = 0; y < height / 2; y++)
            {
                var top = y * stride;
                var bottom = (height - y - 1) * stride;
                Buffer.BlockCopy(pixels, top, row, 0, stride);
                Buffer.BlockCopy(pixels, bottom, pixels, top, stride);
                Buffer.BlockCopy(row, 0, pixels, bottom, stride);
            }
        }
    }
}
