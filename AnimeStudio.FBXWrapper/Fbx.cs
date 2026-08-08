using AnimeStudio.FbxInterop;
using AnimeStudio.PInvoke;
using Newtonsoft.Json.Linq;
using System.IO;

namespace AnimeStudio
{
    public static partial class Fbx
    {

        static Fbx()
        {
            // x64 only, so it lives in the application directory rather than in x86/x64.
            DllLoader.PreloadDll(FbxDll.DllName, archSpecific: false);
        }

        public static Vector3 QuaternionToEuler(Quaternion q)
        {
            AsUtilQuaternionToEuler(q.X, q.Y, q.Z, q.W, out var x, out var y, out var z);
            return new Vector3(x, y, z);
        }

        public static Quaternion EulerToQuaternion(Vector3 v)
        {
            AsUtilEulerToQuaternion(v.X, v.Y, v.Z, out var x, out var y, out var z, out var w);
            return new Quaternion(x, y, z, w);
        }

        public static class Exporter
        {
            public static void Export(string path, IImported imported, ExportOptions exportOptions)
            {
                var file = new FileInfo(path);
                var dir = file.Directory;

                if (!dir.Exists)
                {
                    dir.Create();
                }

                var currentDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(dir.FullName);

                var name = Path.GetFileName(path);

                using (var exporter = new FbxExporter(name, imported, exportOptions))
                {
                    exporter.Initialize();
                    exporter.ExportAll();
                }

                Directory.SetCurrentDirectory(currentDir);
            }
        }

        public record ExportOptions
        {
            public bool eulerFilter;
            public float filterPrecision;
            public bool exportAllNodes;
            public bool exportSkins;
            public bool exportAnimations;
            public bool exportBlendShape;
            public bool castToBone;
            public int boneSize;
            public float scaleFactor;
            public int fbxVersion;
            public int fbxFormat;
        }
    }
}
