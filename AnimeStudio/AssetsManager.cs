using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using static AnimeStudio.ImportHelper;

namespace AnimeStudio
{
    public class AssetsManager
    {
        public Game Game;
        public bool Silent = false;
        public bool SkipProcess = false;
        public bool ResolveDependencies = false;        
        public string SpecifyUnityVersion;
        public CancellationTokenSource tokenSource = new CancellationTokenSource();
        public List<SerializedFile> assetsFileList = new List<SerializedFile>();

        internal Dictionary<string, int> assetsFileIndexCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, BinaryReader> resourceFileReaders = new Dictionary<string, BinaryReader>(StringComparer.OrdinalIgnoreCase);

        internal List<string> importFiles = new List<string>();
        internal HashSet<string> importFilesHash = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal HashSet<string> noexistFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal HashSet<string> assetsFileListHash = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public class AssetFilterDataItem
        {
            public String Source { get; set; }
            public ClassIDType Type { get; set; }
            public String Name { get; set; }
            public long PathID { get; set; }
            public long Offset { get; set; } = -1;
        }

        public class AssetFilterData
        {
            public List<AssetFilterDataItem> Items { get; set; }
        }

        public class AssetFilterDataItemEqualityComparer : IEqualityComparer<AssetFilterDataItem>
        {
            public bool Equals(AssetFilterDataItem? d1, AssetFilterDataItem? d2)
            {
                if (ReferenceEquals(d1, d2))
                    return true;

                if (d2 is null || d1 is null)
                    return false;

                return d1.Type == d2.Type && d1.PathID == d2.PathID && d1.Name.Equals(d2.Name, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(AssetFilterDataItem d) => HashCode.Combine(d.Name, d.PathID, d.Type);
        }

        public AssetFilterData FilterData = new AssetFilterData { Items = new List<AssetFilterDataItem>() };

        public Dictionary<string, List<long>> OffsetData = new();

        public void LoadFiles(params string[] files)
        {
            if (Silent)
            {
                Logger.Silent = true;
                Progress.Silent = true;
            }

            var path = Path.GetDirectoryName(Path.GetFullPath(files[0]));
            MergeSplitAssets(path);
            var toReadFile = ProcessingSplitFiles(files.ToList());
            if (ResolveDependencies)
                toReadFile = AssetsHelper.ProcessDependencies(toReadFile);
            Load(toReadFile);

            if (Silent)
            {
                Logger.Silent = false;
                Progress.Silent = false;
            }
        }

        public void LoadFolder(string path)
        {
            if (Silent)
            {
                Logger.Silent = true;
                Progress.Silent = true;
            }

            MergeSplitAssets(path, true);
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).ToList();
            var toReadFile = ProcessingSplitFiles(files);
            Load(toReadFile);

            if (Silent)
            {
                Logger.Silent = false;
                Progress.Silent = false;
            }
        }

        private void Load(string[] files)
        {
            foreach (var file in files)
            {
                Logger.Verbose($"caching {file} path and name to filter out duplicates");
                importFiles.Add(file);
                importFilesHash.Add(Path.GetFileName(file));
            }

            Progress.Reset();
            //use a for loop because list size can change
            for (var i = 0; i < importFiles.Count; i++)
            {
                LoadFile(importFiles[i]);
                Progress.Report(i + 1, importFiles.Count);
                if (tokenSource.IsCancellationRequested)
                {
                    Logger.Info("Loading files has been aborted !!");
                    break;
                }
            }

            importFiles.Clear();
            importFilesHash.Clear();
            noexistFiles.Clear();
            assetsFileListHash.Clear();
            AssetsHelper.ClearOffsets();

            if (!SkipProcess)
            {
                ReadAssets();
                ProcessAssets();
            }
        }

        private void LoadFile(string fullName)
        {
            var reader = new FileReader(fullName);
            reader = reader.PreProcessing(Game);
            LoadFile(reader);
        }

        private void LoadFile(FileReader reader)
        {
            OffsetData.Clear();
            if (FilterData.Items.Count > 0)
            {
                var key = reader.FileName;

                if (!OffsetData.TryGetValue(key, out var existingList))
                    existingList = new List<long>();

                var set = new HashSet<long>();
                if (existingList != null)
                    foreach (var off in existingList)
                        set.Add(off);

                foreach (var item in FilterData.Items)
                {
                    if (string.IsNullOrEmpty(item.Source))
                        continue;

                    var itemFileName = Path.GetFileName(item.Source);
                    if (!item.Source.Equals(key, StringComparison.OrdinalIgnoreCase) && !itemFileName.Equals(key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (item.Offset >= 0)
                        set.Add(item.Offset);
                    else
                        if (AssetsHelper.TryGet(item.Source, out var offsets) && offsets.Length > 0)
                        foreach (var off in offsets)
                            set.Add(off);
                }

                OffsetData[key] = set.ToList();
            }

            switch (reader.FileType)
            {
                case FileType.AssetsFile:
                    LoadAssetsFile(reader);
                    break;
                case FileType.BundleFile:
                    LoadGameBlockFile(reader);
                    break;
                case FileType.WebFile:
                    LoadWebFile(reader);
                    break;
                case FileType.GZipFile:
                    LoadFile(DecompressGZip(reader));
                    break;
                case FileType.BrotliFile:
                    LoadFile(DecompressBrotli(reader));
                    break;
                case FileType.ZipFile:
                    LoadZipFile(reader);
                    break;
                case FileType.BlockFile:
                case FileType.BlkFile:
                    LoadBlockFile(reader);
                    break;
                case FileType.MhyFile:
                    LoadGameBlockFile(reader);
                    break;
            }
        }

        private void LoadAssetsFile(FileReader reader)
        {
            if (!assetsFileListHash.Contains(reader.FileName))
            {
                Logger.Info($"Loading {reader.FullPath}");
                try
                {
                    var assetsFile = new SerializedFile(reader, this);
                    CheckStrippedVersion(assetsFile);
                    assetsFileList.Add(assetsFile);
                    assetsFileIndexCache.Add(assetsFile.fileName, assetsFileList.Count - 1);
                    assetsFileListHash.Add(assetsFile.fileName);

                    foreach (var sharedFile in assetsFile.m_Externals)
                    {
                        Logger.Verbose($"{assetsFile.fileName} needs external file {sharedFile.fileName}, attempting to look it up...");
                        var sharedFileName = sharedFile.fileName;

                        if (!importFilesHash.Contains(sharedFileName))
                        {
                            var sharedFilePath = Path.Combine(Path.GetDirectoryName(reader.FullPath), sharedFileName);
                            if (!noexistFiles.Contains(sharedFilePath))
                            {
                                if (!File.Exists(sharedFilePath))
                                {
                                    var findFiles = Directory.GetFiles(Path.GetDirectoryName(reader.FullPath), sharedFileName, SearchOption.AllDirectories);
                                    if (findFiles.Length > 0)
                                    {
                                        Logger.Verbose($"Found {findFiles.Length} matching files, picking first file {findFiles[0]} !!");
                                        sharedFilePath = findFiles[0];
                                    }
                                }
                                if (File.Exists(sharedFilePath))
                                {
                                    importFiles.Add(sharedFilePath);
                                    importFilesHash.Add(sharedFileName);
                                }
                                else
                                {
                                    Logger.Verbose("Nothing was found, caching into non existant files to avoid repeated searching !!");
                                    noexistFiles.Add(sharedFilePath);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"Error while reading assets file {reader.FullPath}", e);
                    reader.Dispose();
                }
            }
            else
            {
                Logger.Info($"Skipping {reader.FullPath}");
                reader.Dispose();
            }
        }

        private void LoadAssetsFromMemory(FileReader reader, string originalPath, string unityVersion = null, long originalOffset = 0)
        {
            Logger.Verbose($"Loading asset file {reader.FileName} with version {unityVersion} from {originalPath} at offset 0x{originalOffset:X8}");
            if (!assetsFileListHash.Contains(reader.FileName))
            {
                try
                {
                    var assetsFile = new SerializedFile(reader, this);
                    assetsFile.originalPath = originalPath;
                    assetsFile.offset = originalOffset;
                    if (!string.IsNullOrEmpty(unityVersion) && assetsFile.header.m_Version < SerializedFileFormatVersion.Unknown_7)
                    {
                        assetsFile.SetVersion(unityVersion);
                    }
                    CheckStrippedVersion(assetsFile);
                    assetsFileList.Add(assetsFile);
                    assetsFileIndexCache.Add(assetsFile.fileName, assetsFileList.Count - 1);
                    assetsFileListHash.Add(assetsFile.fileName);
                }
                catch (Exception e)
                {
                    Logger.Error($"Error while reading assets file {reader.FullPath} from {Path.GetFileName(originalPath)}", e);
                    resourceFileReaders.TryAdd(reader.FileName, reader);
                }
            }
            else
                Logger.Info($"Skipping {originalPath} ({reader.FileName})");
        }

        private void LoadWebFile(FileReader reader)
        {
            Logger.Info("Loading " + reader.FullPath);
            try
            {
                var webFile = new WebFile(reader);
                foreach (var file in webFile.fileList)
                {
                    var dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), file.fileName);
                    var subReader = new FileReader(dummyPath, file.stream);
                    switch (subReader.FileType)
                    {
                        case FileType.AssetsFile:
                            LoadAssetsFromMemory(subReader, reader.FullPath);
                            break;
                        case FileType.BundleFile:
                            LoadGameBlockFile(subReader, reader.FullPath);
                            break;
                        case FileType.WebFile:
                            LoadWebFile(subReader);
                            break;
                        case FileType.ResourceFile:
                            Logger.Verbose("Caching resource stream");
                            resourceFileReaders.TryAdd(file.fileName, subReader); //TODO
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Error while reading web file {reader.FullPath}", e);
            }
            finally
            {
                reader.Dispose();
            }
        }

        private void LoadZipFile(FileReader reader)
        {
            Logger.Info("Loading " + reader.FileName);
            try
            {
                using (ZipArchive archive = new ZipArchive(reader.BaseStream, ZipArchiveMode.Read))
                {
                    List<string> splitFiles = new List<string>();
                    Logger.Verbose("Register all files before parsing the assets so that the external references can be found and find split files");
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.Name.Contains(".split"))
                        {
                            string baseName = Path.GetFileNameWithoutExtension(entry.Name);
                            string basePath = Path.Combine(Path.GetDirectoryName(entry.FullName), baseName);
                            if (!splitFiles.Contains(basePath))
                            {
                                splitFiles.Add(basePath);
                                importFilesHash.Add(baseName);
                            }
                        }
                        else
                        {
                            importFilesHash.Add(entry.Name);
                        }
                    }

                    Logger.Verbose("Merge split files and load the result");
                    foreach (string basePath in splitFiles)
                    {
                        try
                        {
                            Stream splitStream = new MemoryStream();
                            int i = 0;
                            while (true)
                            {
                                string path = $"{basePath}.split{i++}";
                                ZipArchiveEntry entry = archive.GetEntry(path);
                                if (entry == null)
                                    break;
                                using (Stream entryStream = entry.Open())
                                {
                                    entryStream.CopyTo(splitStream);
                                }
                            }
                            splitStream.Seek(0, SeekOrigin.Begin);
                            FileReader entryReader = new FileReader(basePath, splitStream);
                            entryReader = entryReader.PreProcessing(Game);
                            LoadFile(entryReader);
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Error while reading zip split file {basePath}", e);
                        }
                    }

                    Logger.Verbose("Load all entries");
                    Logger.Verbose($"Found {archive.Entries.Count} entries"); 
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        try
                        {
                            string dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), reader.FileName, entry.FullName);
                            Logger.Verbose("Create a new stream to store the deflated stream in and keep the data for later extraction");
                            Stream streamReader = new MemoryStream();
                            using (Stream entryStream = entry.Open())
                            {
                                entryStream.CopyTo(streamReader);
                            }
                            streamReader.Position = 0;

                            FileReader entryReader = new FileReader(dummyPath, streamReader);
                            entryReader = entryReader.PreProcessing(Game);
                            LoadFile(entryReader);
                            if (entryReader.FileType == FileType.ResourceFile)
                            {
                                entryReader.Position = 0;
                                Logger.Verbose("Caching resource file");
                                resourceFileReaders.TryAdd(entry.Name, entryReader);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Error while reading zip entry {entry.FullName}", e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Error while reading zip file {reader.FileName}", e);
            }
            finally
            {
                reader.Dispose();
            }
        }
        private void LoadBlockFile(FileReader reader)
        {
            Logger.Info("Loading " + reader.FullPath);
            try
            {
                dynamic stream;

                switch (reader.FileType)
                {
                    case FileType.BlkFile:
                        stream = BlkUtils.Decrypt(reader, (Blk)Game);
                        break;
                    default:
                        stream = new OffsetStream(reader.BaseStream, 0);
                        break;
                }

                Progress.Reset();
                using (stream)
                {
                    var total = stream.Length;

                    OffsetData.TryGetValue(reader.FileName, out var manualOffsets);
                    bool isManualOffsets = (manualOffsets != null && manualOffsets.Count > 0);
                    IEnumerable<long> offsetsEnumerable = isManualOffsets
                        ? manualOffsets
                        : stream.GetOffsets(reader.FullPath);

                    int idx = 0;
                    int? manualTotal = (manualOffsets != null && manualOffsets.Count > 0) ? manualOffsets.Count : (int?)null;
                    foreach (var offset in offsetsEnumerable)
                    {
                        var name = offset.ToString("X8");
                        Logger.Verbose($"Loading Block {name}");

                        var dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), name);
                        var subReader = new FileReader(dummyPath, stream, true);
                        if (isManualOffsets)
                            subReader.Position = offset;
                        LoadGameBlockFile(subReader, reader.FullPath, offset, false);

                        if (manualTotal.HasValue)
                            Progress.Report(idx + 1, manualTotal.Value);
                        else
                            Progress.Report((int)offset, (int)total);
                        idx++;
                    }
                }
                
            }
            catch (Exception e)
            {
                Logger.Error($"Error while reading block file {reader.FileName}", e);
            }
            finally
            {
                reader.Dispose();
            }
        }
        private void LoadGameBlockFile(FileReader reader, string originalPath = null, long originalOffset = 0, bool log = true)
        {
            if (log)
            {
                Logger.Info("Loading " + reader.FullPath);
            }
            try
            {
                dynamic file = null;

                switch (reader.FileType)
                {
                    case FileType.ENCRFile:
                    case FileType.BundleFile:
                        file = new BundleFile(reader, Game);
                        break;
                    case FileType.Blb3File:
                        file = new Blb3File(reader, reader.FullPath);
                        break;
                    case FileType.MhyFile:
                        file = new MhyFile(reader, (Mhy)Game);
                        break;
                    case FileType.HygFile:
                        file = new HygFile(reader, reader.FullPath);
                        break;
                    case FileType.VFSFile:
                        file = new VFSFile(reader, reader.FullPath);
                        break;
                }

                if (file == null)
                    throw new Exception("Unsupported game block file type");

                Logger.Verbose($"file total size: {file.m_Header.size:X8}");
                foreach (var innerFile in file.fileList)
                {
                    var dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), innerFile.fileName);
                    var cabReader = new FileReader(dummyPath, innerFile.stream);
                    if (cabReader.FileType == FileType.AssetsFile)
                    {
                        LoadAssetsFromMemory(cabReader, originalPath ?? reader.FullPath, file.m_Header.unityRevision, originalOffset);
                    }
                    else
                    {
                        Logger.Verbose("Caching resource stream");
                        resourceFileReaders.TryAdd(innerFile.fileName, cabReader); //TODO
                    }
                }
            }
            catch (InvalidCastException)
            {
                string name = "";
                switch (reader.FileType)
                {
                    case FileType.ENCRFile:
                    case FileType.BundleFile:
                        name = nameof(Mr0k);
                        break;
                    case FileType.Blb3File:
                        name = nameof(Blb3File);
                        break;
                    case FileType.MhyFile:
                        name = nameof(Mhy);
                        break;
                    case FileType.HygFile:
                        name = nameof(HygFile);
                        break;
                    case FileType.VFSFile:
                        name = nameof(VFSFile);
                        break;
                }
                Logger.Error($"Game type mismatch, Expected {name} but got {Game.Name} ({Game.GetType().Name}) !!");
            }
            catch (Exception e)
            {
                var str = $"Error while reading file {reader.FullPath}";
                if (originalPath != null)
                {
                    str += $" from {Path.GetFileName(originalPath)}";
                }
                Logger.Error(str, e);
            }
            finally
            {
                reader.Dispose();
            }
        }

        public void CheckStrippedVersion(SerializedFile assetsFile)
        {
            if (assetsFile.IsVersionStripped && string.IsNullOrEmpty(SpecifyUnityVersion))
            {
                throw new Exception("The Unity version has been stripped, please set the version in the options");
            }
            if (!string.IsNullOrEmpty(SpecifyUnityVersion))
            {
                assetsFile.SetVersion(SpecifyUnityVersion);
            }
        }

        public void Clear()
        {
            Logger.Verbose("Cleaning up...");

            foreach (var assetsFile in assetsFileList)
            {
                assetsFile.Objects.Clear();
                assetsFile.reader.Close();
            }
            assetsFileList.Clear();

            foreach (var resourceFileReader in resourceFileReaders)
            {
                resourceFileReader.Value.Close();
            }
            resourceFileReaders.Clear();

            assetsFileIndexCache.Clear();

            tokenSource.Dispose();
            tokenSource = new CancellationTokenSource();

            // GC.WaitForPendingFinalizers();
            // GC.Collect();
        }

        private void ReadAssets()
        {
            Logger.Info("Read assets...");

            var progressCount = assetsFileList.Sum(x => x.m_Objects.Count);
            int i = 0;
            Progress.Reset();
            foreach (var assetsFile in assetsFileList)
            {
                foreach (var objectInfo in assetsFile.m_Objects)
                {
                    if (tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("Reading assets has been cancelled !!");
                        return;
                    }
                    var objectReader = new ObjectReader(assetsFile.reader, assetsFile, objectInfo, Game);
                    try
                    {
                        Object obj = objectReader.type switch
                        {
                            ClassIDType.Animation when ClassIDType.Animation.CanParse() => new Animation(objectReader),
                            ClassIDType.AnimationClip when ClassIDType.AnimationClip.CanParse() => new AnimationClip(objectReader),
                            ClassIDType.Animator when ClassIDType.Animator.CanParse() => new Animator(objectReader),
                            ClassIDType.AnimatorController when ClassIDType.AnimatorController.CanParse() => new AnimatorController(objectReader),
                            ClassIDType.AnimatorOverrideController when ClassIDType.AnimatorOverrideController.CanParse() => new AnimatorOverrideController(objectReader),
                            ClassIDType.AssetBundle when ClassIDType.AssetBundle.CanParse() => new AssetBundle(objectReader),
                            ClassIDType.AudioClip when ClassIDType.AudioClip.CanParse() => new AudioClip(objectReader),
                            ClassIDType.Avatar when ClassIDType.Avatar.CanParse() => new Avatar(objectReader),
                            ClassIDType.Font when ClassIDType.Font.CanParse() => new Font(objectReader),
                            ClassIDType.GameObject when ClassIDType.GameObject.CanParse() => new GameObject(objectReader),
                            ClassIDType.IndexObject when ClassIDType.IndexObject.CanParse() => new IndexObject(objectReader),
                            ClassIDType.Material when ClassIDType.Material.CanParse() => new Material(objectReader),
                            ClassIDType.Mesh when ClassIDType.Mesh.CanParse() => new Mesh(objectReader),
                            ClassIDType.MeshFilter when ClassIDType.MeshFilter.CanParse() => new MeshFilter(objectReader),
                            ClassIDType.MeshRenderer when ClassIDType.MeshRenderer.CanParse() => new MeshRenderer(objectReader),
                            ClassIDType.MiHoYoBinData when ClassIDType.MiHoYoBinData.CanParse() => new MiHoYoBinData(objectReader),
                            ClassIDType.MonoBehaviour when ClassIDType.MonoBehaviour.CanParse() => new MonoBehaviour(objectReader),
                            ClassIDType.MonoScript when ClassIDType.MonoScript.CanParse() => new MonoScript(objectReader),
                            ClassIDType.MovieTexture when ClassIDType.MovieTexture.CanParse() => new MovieTexture(objectReader),
                            ClassIDType.PlayerSettings when ClassIDType.PlayerSettings.CanParse() => new PlayerSettings(objectReader),
                            ClassIDType.RectTransform when ClassIDType.RectTransform.CanParse() => new RectTransform(objectReader),
                            ClassIDType.Shader when ClassIDType.Shader.CanParse() => new Shader(objectReader),
                            ClassIDType.SkinnedMeshRenderer when ClassIDType.SkinnedMeshRenderer.CanParse() => new SkinnedMeshRenderer(objectReader),
                            ClassIDType.Sprite when ClassIDType.Sprite.CanParse() => new Sprite(objectReader),
                            ClassIDType.SpriteAtlas when ClassIDType.SpriteAtlas.CanParse() => new SpriteAtlas(objectReader),
                            ClassIDType.TextAsset when ClassIDType.TextAsset.CanParse() => new TextAsset(objectReader),
                            ClassIDType.Texture2D when ClassIDType.Texture2D.CanParse() => new Texture2D(objectReader),
                            ClassIDType.Transform when ClassIDType.Transform.CanParse() => new Transform(objectReader),
                            ClassIDType.VideoClip when ClassIDType.VideoClip.CanParse() => new VideoClip(objectReader),
                            ClassIDType.ResourceManager when ClassIDType.ResourceManager.CanParse() => new ResourceManager(objectReader),
                            ClassIDType.NapAssetBundleIndexAsset when ClassIDType.NapAssetBundleIndexAsset.CanParse() => new NapAssetBundleIndexAsset(objectReader),
                            _ => new Object(objectReader),
                        };
                        assetsFile.AddObject(obj);
                    }
                    catch (Exception e)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Unable to load object")
                            .AppendLine($"Assets {assetsFile.fileName}")
                            .AppendLine($"Path {assetsFile.originalPath}")
                            .AppendLine($"Type {objectReader.type}")
                            .AppendLine($"PathID {objectInfo.m_PathID}")
                            .Append(e);
                        Logger.Error(sb.ToString());
                    }

                    Progress.Report(++i, progressCount);
                }
            }
        }

        private void ProcessAssets()
        {
            Logger.Info("Process Assets...");

            var separateMeshes = new Dictionary<string, PPtr<Mesh>>();
            var avatars = new List<GameObject>();
            var fileID = 0;

            if (Game.Type.IsZZZGroup())
            {   
                // TODO: Refactor this to decrease the number of meshes. Possibly do this after we build the hierarchy to discover unused meshes (which are likely to be SeparateMeshes...)
                // TODO: Somehow RE the behavior used to swap meshes to determine exact mappings instead of guessing by name...
                foreach (var assetsFile in assetsFileList)
                {
                    foreach (var obj in assetsFile.Objects)
                    {
                        if (tokenSource.IsCancellationRequested)
                        {
                            Logger.Info("Processing assets has been cancelled !!");
                            return;
                        }
                        if (obj.type == ClassIDType.Mesh)
                        {
                            var pptr = new PPtr<Mesh>(0, obj.m_PathID, assetsFile);
                            if (pptr.TryGet(out var mesh))
                            {
                                if (separateMeshes.ContainsKey(obj.Name))
                                {
                                    // Logger.Warning($"Found possible duplicate SeparateMesh: {obj.Name}, {mesh.Name}");
                                }
                                else
                                {
                                    Logger.Verbose($"Found SeparateMesh {mesh.Name}");
                                    separateMeshes.Add(obj.Name, pptr);
                                }
                            }
                            else
                            {
                                throw new Exception($"Invalid PPtr for {obj.Name}");
                            }
                        }
                    }
                    fileID++;
                }
                Logger.Info($"Found {separateMeshes.Count} SeparateMeshes");
            }
            
            foreach (var assetsFile in assetsFileList)
            {
                foreach (var obj in assetsFile.Objects)
                {
                    if (tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("Processing assets has been cancelled !!");
                        return;
                    }
                    if (obj is GameObject m_GameObject)
                    {
                        Logger.Verbose($"GameObject with {m_GameObject.m_PathID} in file {m_GameObject.assetsFile.fileName} has {m_GameObject.m_Components.Count} components, Attempting to fetch them...");
                        foreach (var pptr in m_GameObject.m_Components)
                        {
                            if (pptr.TryGet(out var m_Component))
                            {
                                switch (m_Component)
                                {
                                    case Transform m_Transform:
                                        Logger.Verbose($"Fetched Transform component with {m_Transform.m_PathID} in file {m_Transform.assetsFile.fileName}, assigning to GameObject components...");
                                        m_GameObject.m_Transform = m_Transform;
                                            break;
                                    case MeshRenderer m_MeshRenderer:
                                        Logger.Verbose($"Fetched MeshRenderer component with {m_MeshRenderer.m_PathID} in file {m_MeshRenderer.assetsFile.fileName}, assigning to GameObject components...");
                                        m_GameObject.m_MeshRenderer = m_MeshRenderer;
                                            break;
                                    case MeshFilter m_MeshFilter:
                                        Logger.Verbose($"Fetched MeshFilter component with {m_MeshFilter.m_PathID} in file {m_MeshFilter.assetsFile.fileName}, assigning to GameObject components...");
                                        m_GameObject.m_MeshFilter = m_MeshFilter;
                                            break;
                                    case SkinnedMeshRenderer m_SkinnedMeshRenderer:
                                        Logger.Verbose($"Fetched SkinnedMeshRenderer component with {m_SkinnedMeshRenderer.m_PathID} in file {m_SkinnedMeshRenderer.assetsFile.fileName}, assigning to GameObject components...");
                                        m_GameObject.m_SkinnedMeshRenderer = m_SkinnedMeshRenderer;
                                            break;
                                    case Animator m_Animator:
                                        Logger.Verbose($"Fetched Animator component with {m_Animator.m_PathID} in file {m_Animator.assetsFile.fileName}, assigning to GameObject components...");
                                        m_GameObject.m_Animator = m_Animator;
                                            break;
                                    case Animation m_Animation:
                                        Logger.Verbose($"Fetched Animation component with {m_Animation.m_PathID} in file {m_Animation.assetsFile.fileName}, assigning to GameObject components...");
                                        m_GameObject.m_Animation = m_Animation;
                                            break;
                                }
                            }
                        }
                        if (Game.Type.IsZZZGroup() && (m_GameObject.m_Animator != null || m_GameObject.Name.StartsWith("Avatar_") || m_GameObject.Name.EndsWith("_Model")))
                        {
                            avatars.Add(m_GameObject);
                        }
                    }
                    else if (obj is SpriteAtlas m_SpriteAtlas)
                    {
                        if (m_SpriteAtlas.m_RenderDataMap.Count > 0)
                        {
                            Logger.Verbose($"SpriteAtlas with {m_SpriteAtlas.m_PathID} in file {m_SpriteAtlas.assetsFile.fileName} has {m_SpriteAtlas.m_PackedSprites.Count} packed sprites, Attempting to fetch them...");
                            foreach (var m_PackedSprite in m_SpriteAtlas.m_PackedSprites)
                            {
                                if (m_PackedSprite.TryGet(out var m_Sprite))
                                {
                                    if (m_Sprite.m_SpriteAtlas.IsNull)
                                    {
                                        Logger.Verbose($"Fetched Sprite with {m_Sprite.m_PathID} in file {m_Sprite.assetsFile.fileName}, assigning to parent SpriteAtlas...");
                                        m_Sprite.m_SpriteAtlas.Set(m_SpriteAtlas);
                                    }
                                    else
                                    {
                                        m_Sprite.m_SpriteAtlas.TryGet(out var m_SpriteAtlaOld);
                                        if (m_SpriteAtlaOld.m_IsVariant)
                                        {
                                            Logger.Verbose($"Fetched Sprite with {m_Sprite.m_PathID} in file {m_Sprite.assetsFile.fileName} has a variant of the origianl SpriteAtlas, disposing of the variant and assinging to the parent SpriteAtlas...");
                                            m_Sprite.m_SpriteAtlas.Set(m_SpriteAtlas);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (Game.Type.IsZZZGroup())
            {
                Logger.Info($"Found {avatars.Count} Avatars");
                foreach (var avatar in avatars)
                {
                    var rootName = avatar.Name;
                    Logger.Verbose($"Attempting to process SeparateMesh for {rootName}");


                    if (avatar.m_Transform != null)
                    {
                        foreach (var childPtr in avatar.m_Transform.m_Children)
                        {
                            if (childPtr.TryGet(out var child) && child.m_GameObject.TryGet(out var childGO))
                            {
                                var childName = childGO.Name;
                                foreach (var i in childGO.m_Components)
                                {
                                    if (i.TryGet<MonoBehaviour>(out var comp))
                                    {
                                        if(comp.Name == "NapLodController")
                                        {
                                            // Safely decode raw bytes to string
                                            var raw = comp.GetRawData();
                                            string Path = raw != null ? System.Text.Encoding.UTF8.GetString(raw) : string.Empty;
                                            if (string.IsNullOrEmpty(Path))
                                                continue;

                                            int assetIndex = Path.IndexOf("Assets", StringComparison.Ordinal);

                                            string trimmed;
                                            if (assetIndex != -1)
                                            {
                                                // Return the substring starting from the found index
                                                trimmed = Path.Substring(assetIndex);
                                            }
                                            else if (Path.Length > 40)
                                            {
                                                // only take substring if long enough
                                                trimmed = Path.Substring(40);
                                            }
                                            else
                                            {
                                                // too short to be useful
                                                Logger.Verbose($"NapLodController path too short ({Path.Length}), skipping");
                                                continue;
                                            }

                                            trimmed = trimmed?.Trim();
                                            if (string.IsNullOrEmpty(trimmed))
                                                continue;

                                            // ensure ".mesh" exists before taking substring
                                            int meshIndex = trimmed.IndexOf(".mesh", StringComparison.OrdinalIgnoreCase);
                                            if (meshIndex <= 0)
                                            {
                                                Logger.Verbose($".mesh not found in '{trimmed}', skipping");
                                                continue;
                                            }

                                            trimmed = trimmed.Substring(0, meshIndex);

                                            // safely get last token after '/', if present
                                            int lastSlash = trimmed.LastIndexOf('/');
                                            if (lastSlash >= 0 && lastSlash < trimmed.Length - 1)
                                                trimmed = trimmed.Substring(lastSlash + 1);

                                            trimmed = trimmed.Trim();
                                            if (string.IsNullOrEmpty(trimmed))
                                                continue;


                                            if (separateMeshes.TryGetValue(trimmed, out var meshPPtr))
                                            {
                                                Logger.Verbose($"Trying to attach {trimmed} to {childName}");
                                                if (childGO.m_SkinnedMeshRenderer != null && childGO.m_SkinnedMeshRenderer.m_Mesh.IsNull)
                                                {
                                                    Logger.Info($"Attached {trimmed} to {childName}");
                                                    childGO.m_SkinnedMeshRenderer.m_Mesh = meshPPtr;
                                                }
                                                else if (childGO.m_MeshFilter != null && childGO.m_MeshFilter.m_Mesh.IsNull)
                                                {
                                                    Logger.Info($"Attached {trimmed} to {childName}");
                                                    childGO.m_MeshFilter.m_Mesh = meshPPtr;
                                                }
                                            }
                                            else if (separateMeshes.TryGetValue(childName, out meshPPtr))
                                            {
                                                Logger.Verbose($"Trying to attach {childName} to {childName}");
                                                if (childGO.m_SkinnedMeshRenderer != null && childGO.m_SkinnedMeshRenderer.m_Mesh.IsNull)
                                                {
                                                    Logger.Info($"Attached {childName} to {childName}");
                                                    childGO.m_SkinnedMeshRenderer.m_Mesh = meshPPtr;
                                                }
                                                else if (childGO.m_MeshFilter != null && childGO.m_MeshFilter.m_Mesh.IsNull)
                                                {
                                                    Logger.Info($"Attached {childName} to {childName}");
                                                    childGO.m_MeshFilter.m_Mesh = meshPPtr;
                                                }
                                            }

                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
