using AnimeStudio;
using AnimeStudio.GUI;
using L2DSpineExporter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace UnityLive2DExtractor
{
    internal class Live2DExtractor
    {
        public static void CreateMotion3Json(GameObject gameObject, List<AnimationClip> animationClips, Action<GameObject, string, string> JsonMap)
        {
            var rootTransform = gameObject.m_Transform;
            while (rootTransform.m_Father.TryGet(out var m_Father))
            {
                rootTransform = m_Father;
            }
            rootTransform.m_GameObject.TryGet(out var rootGameObject);
            var converter = new CubismMotion3Converter(rootGameObject, animationClips.ToArray());
            foreach (ImportedKeyframedAnimation animation in converter.AnimationList)
            {
                var json = new CubismMotion3Json
                {
                    Version = 3,
                    Meta = new CubismMotion3Json.SerializableMeta
                    {
                        Duration = animation.Duration,
                        Fps = animation.SampleRate,
                        Loop = true,
                        AreBeziersRestricted = true,
                        CurveCount = animation.TrackList.Count,
                        UserDataCount = animation.Events.Count
                    },
                    Curves = new CubismMotion3Json.SerializableCurve[animation.TrackList.Count]
                };
                int totalSegmentCount = 1;
                int totalPointCount = 1;
                for (int i = 0; i < animation.TrackList.Count; i++)
                {
                    var track = animation.TrackList[i];
                    json.Curves[i] = new CubismMotion3Json.SerializableCurve
                    {
                        Target = track.Target,
                        Id = track.Name,
                        Segments = new List<float> { 0f, track.Curve[0].value }
                    };
                    for (var j = 1; j < track.Curve.Count; j++)
                    {
                        var curve = track.Curve[j];
                        var preCurve = track.Curve[j - 1];
                        if (Math.Abs(curve.time - preCurve.time - 0.01f) < 0.0001f) //InverseSteppedSegment
                        {
                            var nextCurve = track.Curve[j + 1];
                            if (nextCurve.value == curve.value)
                            {
                                json.Curves[i].Segments.Add(3f);
                                json.Curves[i].Segments.Add(nextCurve.time);
                                json.Curves[i].Segments.Add(nextCurve.value);
                                j += 1;
                                totalPointCount += 1;
                                totalSegmentCount++;
                                continue;
                            }
                        }
                        if (float.IsPositiveInfinity(curve.inSlope)) //SteppedSegment
                        {
                            json.Curves[i].Segments.Add(2f);
                            json.Curves[i].Segments.Add(curve.time);
                            json.Curves[i].Segments.Add(curve.value);
                            totalPointCount += 1;
                        }
                        else if (preCurve.outSlope == 0f && Math.Abs(curve.inSlope) < 0.0001f) //LinearSegment
                        {
                            json.Curves[i].Segments.Add(0f);
                            json.Curves[i].Segments.Add(curve.time);
                            json.Curves[i].Segments.Add(curve.value);
                            totalPointCount += 1;
                        }
                        else //BezierSegment
                        {
                            var tangentLength = (curve.time - preCurve.time) / 3f;
                            json.Curves[i].Segments.Add(1f);
                            json.Curves[i].Segments.Add(preCurve.time + tangentLength);
                            json.Curves[i].Segments.Add(preCurve.outSlope * tangentLength + preCurve.value);
                            json.Curves[i].Segments.Add(curve.time - tangentLength);
                            json.Curves[i].Segments.Add(curve.value - curve.inSlope * tangentLength);
                            json.Curves[i].Segments.Add(curve.time);
                            json.Curves[i].Segments.Add(curve.value);
                            totalPointCount += 3;
                        }
                        totalSegmentCount++;
                    }
                }
                json.Meta.TotalSegmentCount = totalSegmentCount;
                json.Meta.TotalPointCount = totalPointCount;

                json.UserData = new CubismMotion3Json.SerializableUserData[animation.Events.Count];
                var totalUserDataSize = 0;
                for (var i = 0; i < animation.Events.Count; i++)
                {
                    var @event = animation.Events[i];
                    json.UserData[i] = new CubismMotion3Json.SerializableUserData
                    {
                        Time = @event.time,
                        Value = @event.value
                    };
                    totalUserDataSize += @event.value.Length;
                }
                json.Meta.TotalUserDataSize = totalUserDataSize;
                JsonMap?.Invoke(gameObject, animation.Name, JsonConvert.SerializeObject(json, Formatting.Indented, new MyJsonConverter()));

                // motions.Add($"motions/{animation.Name}.motion3.json");
                //File.WriteAllText($"{destAnimationPath}{animation.Name}.motion3.json", JsonConvert.SerializeObject(json, Formatting.Indented, new MyJsonConverter()));
            }
        }

        public static void CreateMotion3Json(Animator animator, List<AnimationClip> animationClips, Action<GameObject, string, string> JsonMap)
        {
            if (animator.m_GameObject.TryGet(out GameObject gameObject))
            {
                CreateMotion3Json(gameObject, animationClips, JsonMap);
            }
        }

        public static void Extract(AssetsManager assetsManager, string folderPath,ToolStripStatusLabel toolStripStatusLabel)
        {
            Console.WriteLine($"Loading...");
            toolStripStatusLabel.Text = $"Loading...";
            //edited
            if (assetsManager.assetsFileList.Count == 0)
                return;
            var containers = new Dictionary<AnimeStudio.Object, string>();
            var cubismMocs = new List<MonoBehaviour>();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                foreach (var asset in assetsFile.Objects)
                {
                    switch (asset)
                    {
                        case MonoBehaviour m_MonoBehaviour:
                            if (m_MonoBehaviour.m_Script.TryGet(out var m_Script))
                            {
                                if (m_Script.m_ClassName == "CubismMoc")
                                {
                                    cubismMocs.Add(m_MonoBehaviour);
                                }
                            }
                            break;

                        case AssetBundle m_AssetBundle:
                            foreach (var m_Container in m_AssetBundle.m_Container)
                            {
                                var preloadIndex = m_Container.Value.preloadIndex;
                                var preloadSize = m_Container.Value.preloadSize;
                                var preloadEnd = preloadIndex + preloadSize;
                                for (int k = preloadIndex; k < preloadEnd; k++)
                                {
                                    var pptr = m_AssetBundle.m_PreloadTable[k];
                                    if (pptr.TryGet(out var obj))
                                    {
                                        containers[obj] = m_Container.Key;
                                    }
                                }
                            }
                            break;

                        case ResourceManager m_ResourceManager:
                            foreach (var m_Container in m_ResourceManager.m_Container)
                            {
                                if (m_Container.Value.TryGet(out var obj))
                                {
                                    containers[obj] = m_Container.Key;
                                }
                            }
                            break;
                    }
                }
            }
            var basePathList = new List<string>();
            foreach (var cubismMoc in cubismMocs)
            {
                var container = containers[cubismMoc];
                var basePath = container.Substring(0, container.LastIndexOf("/"));
                basePathList.Add(basePath);
            }
            var lookup = containers.ToLookup(x => basePathList.Find(b => x.Value.Contains(b)), x => x.Key);
            var baseDestPath = Path.Combine(folderPath, "Live2DOutput");//edited
            foreach (var assets in lookup)
            {
                var key = assets.Key;
                if (key == null)
                    continue;
                var name = key.Substring(key.LastIndexOf("/") + 1);
                Console.WriteLine($"Extract {key}");
                toolStripStatusLabel.Text = $"Extract {key}";

                var destPath = Path.Combine(baseDestPath, key) + Path.DirectorySeparatorChar;
                var destTexturePath = Path.Combine(destPath, "textures") + Path.DirectorySeparatorChar;
                var destAnimationPath = Path.Combine(destPath, "motions") + Path.DirectorySeparatorChar;
                Directory.CreateDirectory(destPath);
                Directory.CreateDirectory(destTexturePath);
                Directory.CreateDirectory(destAnimationPath);

                var monoBehaviours = new List<MonoBehaviour>();
                var texture2Ds = new List<Texture2D>();
                var gameObjects = new List<GameObject>();
                var animationClips = new List<AnimationClip>();

                foreach (var asset in assets)
                {
                    if (asset is MonoBehaviour m_MonoBehaviour)
                    {
                        monoBehaviours.Add(m_MonoBehaviour);
                    }
                    else if (asset is Texture2D m_Texture2D)
                    {
                        texture2Ds.Add(m_Texture2D);
                    }
                    else if (asset is GameObject m_GameObject)
                    {
                        gameObjects.Add(m_GameObject);
                    }
                    else if (asset is AnimationClip m_AnimationClip)
                    {
                        animationClips.Add(m_AnimationClip);
                    }
                }

                //physics
                var physics = monoBehaviours.FirstOrDefault(x =>
                {
                    if (x.m_Script.TryGet(out var m_Script))
                    {
                        return m_Script.m_ClassName == "CubismPhysicsController";
                    }
                    return false;
                });
                if (physics != null)
                {
                    File.WriteAllText($"{destPath}{name}.physics3.json", ParsePhysics(physics));
                }
                //moc
                var moc = monoBehaviours.First(x =>
                {
                    if (x.m_Script.TryGet(out var m_Script))
                    {
                        return m_Script.m_ClassName == "CubismMoc";
                    }
                    return false;
                });
                File.WriteAllBytes($"{destPath}{name}.moc3", ParseMoc(moc));
                //texture
                var textures = new SortedSet<string>();
                foreach (var texture2D in texture2Ds)
                {
                    var texture2dConverter = new Texture2DConverter(texture2D);
                    var buff = ArrayPool<byte>.Shared.Rent(texture2D.m_Width * texture2D.m_Height * 4);
                    try
                    {
                        if (texture2dConverter.DecodeTexture2D(buff))
                        {
                            textures.Add($"textures/{texture2D.m_Name}.png");
                            var image = Image.LoadPixelData<Bgra32>(buff, texture2D.m_Width, texture2D.m_Height);
                            using (image)
                            {
                                using var file = File.OpenWrite($"{destTexturePath}{texture2D.m_Name}.png");
                                image.Mutate(x => x.Flip(FlipMode.Vertical));
                                image.WriteToStream(file, ImageFormat.Png);
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buff);
                    }
                }
                //motion
                var motions = new List<string>();
                var rootTransform = gameObjects[0].m_Transform;
                while (rootTransform.m_Father.TryGet(out var m_Father))
                {
                    rootTransform = m_Father;
                }
                rootTransform.m_GameObject.TryGet(out var rootGameObject);
                var converter = new CubismMotion3Converter(rootGameObject, animationClips.ToArray());
                foreach (ImportedKeyframedAnimation animation in converter.AnimationList)
                {
                    var json = new CubismMotion3Json
                    {
                        Version = 3,
                        Meta = new CubismMotion3Json.SerializableMeta
                        {
                            Duration = animation.Duration,
                            Fps = animation.SampleRate,
                            Loop = true,
                            AreBeziersRestricted = true,
                            CurveCount = animation.TrackList.Count,
                            UserDataCount = animation.Events.Count
                        },
                        Curves = new CubismMotion3Json.SerializableCurve[animation.TrackList.Count]
                    };
                    int totalSegmentCount = 1;
                    int totalPointCount = 1;
                    for (int i = 0; i < animation.TrackList.Count; i++)
                    {
                        var track = animation.TrackList[i];
                        json.Curves[i] = new CubismMotion3Json.SerializableCurve
                        {
                            Target = track.Target,
                            Id = track.Name,
                            Segments = new List<float> { 0f, track.Curve[0].value }
                        };
                        for (var j = 1; j < track.Curve.Count; j++)
                        {
                            var curve = track.Curve[j];
                            var preCurve = track.Curve[j - 1];
                            if (Math.Abs(curve.time - preCurve.time - 0.01f) < 0.0001f) //InverseSteppedSegment
                            {
                                var nextCurve = track.Curve[j + 1];
                                if (nextCurve.value == curve.value)
                                {
                                    json.Curves[i].Segments.Add(3f);
                                    json.Curves[i].Segments.Add(nextCurve.time);
                                    json.Curves[i].Segments.Add(nextCurve.value);
                                    j += 1;
                                    totalPointCount += 1;
                                    totalSegmentCount++;
                                    continue;
                                }
                            }
                            if (float.IsPositiveInfinity(curve.inSlope)) //SteppedSegment
                            {
                                json.Curves[i].Segments.Add(2f);
                                json.Curves[i].Segments.Add(curve.time);
                                json.Curves[i].Segments.Add(curve.value);
                                totalPointCount += 1;
                            }
                            else if (preCurve.outSlope == 0f && Math.Abs(curve.inSlope) < 0.0001f) //LinearSegment
                            {
                                json.Curves[i].Segments.Add(0f);
                                json.Curves[i].Segments.Add(curve.time);
                                json.Curves[i].Segments.Add(curve.value);
                                totalPointCount += 1;
                            }
                            else //BezierSegment
                            {
                                var tangentLength = (curve.time - preCurve.time) / 3f;
                                json.Curves[i].Segments.Add(1f);
                                json.Curves[i].Segments.Add(preCurve.time + tangentLength);
                                json.Curves[i].Segments.Add(preCurve.outSlope * tangentLength + preCurve.value);
                                json.Curves[i].Segments.Add(curve.time - tangentLength);
                                json.Curves[i].Segments.Add(curve.value - curve.inSlope * tangentLength);
                                json.Curves[i].Segments.Add(curve.time);
                                json.Curves[i].Segments.Add(curve.value);
                                totalPointCount += 3;
                            }
                            totalSegmentCount++;
                        }
                    }
                    json.Meta.TotalSegmentCount = totalSegmentCount;
                    json.Meta.TotalPointCount = totalPointCount;

                    json.UserData = new CubismMotion3Json.SerializableUserData[animation.Events.Count];
                    var totalUserDataSize = 0;
                    for (var i = 0; i < animation.Events.Count; i++)
                    {
                        var @event = animation.Events[i];
                        json.UserData[i] = new CubismMotion3Json.SerializableUserData
                        {
                            Time = @event.time,
                            Value = @event.value
                        };
                        totalUserDataSize += @event.value.Length;
                    }
                    json.Meta.TotalUserDataSize = totalUserDataSize;

                    motions.Add($"motions/{animation.Name}.motion3.json");
                    File.WriteAllText($"{destAnimationPath}{animation.Name}.motion3.json", JsonConvert.SerializeObject(json, Formatting.Indented, new MyJsonConverter()));
                }
                //model
                var job = new JObject();
                var jarray = new JArray();
                foreach (var motion in motions)
                {
                    var tempjob = new JObject();
                    tempjob["File"] = motion;
                    jarray.Add(tempjob);
                }
                job[""] = jarray;

                var groups = new List<CubismModel3Json.SerializableGroup>();
                var eyeBlinkParameters = monoBehaviours.Where(x =>
                {
                    x.m_Script.TryGet(out var m_Script);
                    return m_Script.m_ClassName == "CubismEyeBlinkParameter";
                }).Select(x =>
                {
                    x.m_GameObject.TryGet(out var m_GameObject);
                    return m_GameObject.m_Name;
                }).ToArray();
                if (eyeBlinkParameters.Length > 0)
                {
                    groups.Add(new CubismModel3Json.SerializableGroup
                    {
                        Target = "Parameter",
                        Name = "EyeBlink",
                        Ids = eyeBlinkParameters
                    });
                }
                var lipSyncParameters = monoBehaviours.Where(x =>
                {
                    x.m_Script.TryGet(out var m_Script);
                    return m_Script.m_ClassName == "CubismMouthParameter";
                }).Select(x =>
                {
                    x.m_GameObject.TryGet(out var m_GameObject);
                    return m_GameObject.m_Name;
                }).ToArray();
                if (lipSyncParameters.Length > 0)
                {
                    groups.Add(new CubismModel3Json.SerializableGroup
                    {
                        Target = "Parameter",
                        Name = "LipSync",
                        Ids = lipSyncParameters
                    });
                }

                var model3 = new CubismModel3Json
                {
                    Version = 3,
                    FileReferences = new CubismModel3Json.SerializableFileReferences
                    {
                        Moc = $"{name}.moc3",
                        Textures = textures.ToArray(),
                        //Physics = $"{name}.physics3.json",
                        Motions = job
                    },
                    Groups = groups.ToArray()
                };
                if (physics != null)
                {
                    model3.FileReferences.Physics = $"{name}.physics3.json";
                }
                File.WriteAllText($"{destPath}{name}.model3.json", JsonConvert.SerializeObject(model3, Formatting.Indented));
            }
            Console.WriteLine("Done!");
            toolStripStatusLabel.Text = $"Done!";
            Console.Read();
        }

        public static void ForceSaveAsMoc3(IEnumerable<AssetItem> assetItems, string folder, bool withPathID)
        {
            assetItems.Apply(asset =>
            {
                if (asset.Type == ClassIDType.MonoBehaviour)
                {
                    var monoBehaviour = asset.Asset as MonoBehaviour;

                    if (TryGetMocByClassName(monoBehaviour, out byte[] moc) || TryGetMocByHeader(monoBehaviour, out moc))
                    {
                        string savePath;
                        if (withPathID)
                        {
                            savePath = Path.Combine(folder, asset.m_PathID.ToString() + ".moc3");
                        }
                        else
                        {
                            string savePathWithOutExt = Path.Combine(folder, Path.GetFileNameWithoutExtension(asset.Name));
                            savePath = savePathWithOutExt + ".moc3";
                            if (File.Exists(savePath))
                            {
                                savePath = savePathWithOutExt + asset + ".moc3";
                            }
                        }

                        File.WriteAllBytes(savePath, moc);
                        Console.WriteLine($"Save {savePath} successfully.");
                    }
                }
            });
            Console.WriteLine("Done!");
        }

        public static void ForceSaveAsPhysics(IEnumerable<AssetItem> assetItems, string folder, bool withPathID)
        {
            assetItems.Apply(asset =>
            {
                if (asset.Type == ClassIDType.MonoBehaviour)
                {
                    var monoBehaviour = asset.Asset as MonoBehaviour;

                    if (TryGetPhysicByClassName(monoBehaviour, out string physicJson) || TryGetPhysicBySubRig(monoBehaviour, out physicJson))
                    {
                        string savePath;
                        if (withPathID)
                        {
                            savePath = Path.Combine(folder, asset.m_PathID.ToString() + ".physics3.json");
                        }
                        else
                        {
                            string savePathWithOutExt = Path.Combine(folder, Path.GetFileNameWithoutExtension(asset.Name));
                            savePath = savePathWithOutExt + ".physics3.json";
                            if (File.Exists(savePath))
                            {
                                savePath = savePathWithOutExt + asset + ".physics3.json";
                            }
                        }

                        File.WriteAllText(savePath, physicJson);
                        Console.WriteLine($"Save {savePath} successfully.");
                    }
                }
            });
            Console.WriteLine("Done!");
        }

        public static void SaveByAnimators(IEnumerable<AssetItem> animators, string folder, bool withPathID)
        {
            animators.Apply(asset =>
            {
                var animator = asset.Asset as Animator;
                if (animator.m_GameObject.TryGet(out var gameObject))
                {
                    bool flag = false;
                    bool withPhysic = false;
                    byte[] mocByte = null;
                    string physicJson = string.Empty;
                    List<AnimationClip> animationClips;
                    foreach (var component in gameObject.m_Components)
                    {
                        if (component.TryGet(out MonoBehaviour monoBehaviour))
                        {
                            var dict = monoBehaviour.ToDictionary();
                            if (!flag && dict.Contains("_moc") && monoBehaviour.TryGetComponent(dict["_moc"] as OrderedDictionary, out MonoBehaviour moc))
                            {
                                flag = true;
                                mocByte = ParseMoc(moc);
                                continue;
                            }
                            if (!withPhysic && dict.Contains("_rig"))
                            {
                                withPhysic = true;
                                physicJson = ParsePhysics(monoBehaviour);
                                continue;
                            }
                            if (flag && withPhysic)
                                break;
                        }
                    }
                    if (flag)
                    {
                        string subfolder = withPathID ? Path.Combine(folder, gameObject.Name, gameObject.m_PathID.ToString()) : Path.Combine(folder, gameObject.Name);
                        Directory.CreateDirectory(subfolder);

                        #region saveMoc3

                        string mocPath = Path.Combine(subfolder, gameObject.Name + ".moc3");
                        File.WriteAllBytes(mocPath, mocByte);
                        Console.WriteLine($"------\nSave {mocPath} successfully.");

                        #endregion saveMoc3

                        #region savePhysic

                        if (withPhysic)
                        {
                            string phyPath = Path.Combine(subfolder, gameObject.Name + ".physics3.json");
                            File.WriteAllText(phyPath, physicJson);
                            Console.WriteLine($"Save {phyPath} successfully.");
                        }

                        #endregion savePhysic

                        #region saveTexture

                        if (gameObject.m_Transform.m_Children.FirstOrDefault(x => x.TryGet(out Transform result) && result.m_GameObject.Name == "Drawables").TryGet(out Transform result))
                        {
                            var textures = result.m_Children.ApplyFunc(x =>
                                (x.TryGet(out Transform artMeshTransform)
                                && artMeshTransform.m_GameObject.TryGet(out GameObject artMesh)
                                && artMesh.m_Components[4].TryGet(out MonoBehaviour cubismRenderer)
                                && cubismRenderer.TryGetRefInfo("_mainTexture", out RefInfo token))
                                ? token : null,
                                true).ToHashSet(new RefInfo())
                                .ApplyFunc(x => x.TryGet(result, out Texture2D tex) ? tex : null, true);
                            string imgRoot = Path.Combine(subfolder, "Textures");
                            Directory.CreateDirectory(imgRoot);
                            textures.SaveTextures(imgRoot,null);
                        }

                        #endregion saveTexture

                        #region saveMotion

                        if (animator.m_Controller.TryGet(out AnimatorController controller))
                        {
                            animationClips = controller.m_AnimationClips.ApplyFunc(x => x.TryGet(out AnimationClip animationClip) ? animationClip : null, true).ToList();
                            var count = animationClips.Count;
                            Console.WriteLine($"Get {count} motions.");
                            if (animationClips.Count > 0)
                            {
                                int i = 0;
                                CreateMotion3Json(gameObject, animationClips, (g, name, m) =>
                                {
                                    string motionRoot = Path.Combine(subfolder, "Motions");
                                    Directory.CreateDirectory(motionRoot);
                                    if (name == "")
                                    {
                                        name = i.ToString();
                                    }
                                    string motionPath = Path.Combine(motionRoot, name + ".motion3.json");
                                    File.WriteAllText(motionPath, m);
                                    Console.WriteLine($"Save {motionPath} successfully.");
                                    i += 1;
                                });
                            }
                        }

                        #endregion saveMotion
                    }
                }
            });
            Console.WriteLine("Done!");
        }

        public static void SaveMotionsBySelectedAnimatorClips(IEnumerable<AssetItem> selected, string folder, bool withPathID)
        {
            List<AnimationClip> animationClips = new List<AnimationClip>();
            List<Animator> animators = new List<Animator>();
            selected.Apply(x =>
            {
                if (x.Type == ClassIDType.AnimationClip)
                {
                    animationClips.Add(x.Asset as AnimationClip);
                }
                else if (x.Type == ClassIDType.Animator)
                {
                    animators.Add(x.Asset as Animator);
                }
            });
            foreach (Animator animator in animators)
            {
                CreateMotion3Json(animator, animationClips,
                    (gameObject, name, json) =>
                    {
                        string savePath = withPathID ? Path.Combine(folder, gameObject.Name, gameObject.m_PathID.ToString(), "Motions", name + ".motion3.json") :
                            Path.Combine(folder, gameObject.Name, "Motions", name + ".motion3.json");
                        Directory.CreateDirectory(Directory.GetParent(savePath).ToString());
                        File.WriteAllText(savePath, json);
                        Console.WriteLine($"Save {savePath} successfully.");
                    }
                );
            }
            Console.WriteLine("Done!");
        }

        public static bool TryGetMocByClassName(MonoBehaviour monoBehaviour, out byte[] moc)
        {
            if (monoBehaviour.m_Script.TryGet(out var m_Script))
            {
                if (m_Script.m_ClassName == "CubismMoc")
                {
                    moc = ParseMoc(monoBehaviour);
                    return true;
                }
            }
            moc = null;
            return false;
        }

        public static bool TryGetMocByHeader(MonoBehaviour monoBehaviour, out byte[] moc)
        {
            var reader = monoBehaviour.reader;
            reader.Reset();
            reader.Position += 28; //PPtr<GameObject> m_GameObject, m_Enabled, PPtr<MonoScript>
            reader.ReadAlignedString(); //m_Name
            var length = reader.ReadInt32();
            var signature = Encoding.UTF8.GetString(reader.ReadBytes(4)); //MOC3
            if (signature == "MOC3")
            {
                reader.Position -= 4;
                moc = reader.ReadBytes(length);
                return true;
            }
            moc = null;
            return false;
        }

        public static bool TryGetPhysicByClassName(MonoBehaviour monoBehaviour, out string physic)
        {
            if (monoBehaviour.m_Script.TryGet(out var m_Script))
            {
                if (m_Script.m_ClassName == "CubismPhysicsController")
                {
                    physic = ParsePhysics(monoBehaviour);
                    return true;
                }
            }
            physic = null;
            return false;
        }

        public static bool TryGetPhysicBySubRig(MonoBehaviour monoBehaviour, out string physic)
        {
            SerializedType serializedType = monoBehaviour.reader.serializedType;
            TypeTree type;
            if (serializedType?.m_Type != null)
            {
                type = serializedType.m_Type;
            }
            else
            {
                type = monoBehaviour.ToTypeTree();
            }
            TypeTreeNode withSubRig = type.m_Nodes.FirstOrDefault(x => x.m_Name == "SubRigs");//_rig;SubRigs
            if (withSubRig != null)
            {
                physic = ParsePhysics(monoBehaviour);
                return true;
            }
            physic = string.Empty;
            return false;
        }

        private static byte[] ParseMoc(MonoBehaviour moc)
        {
            var reader = moc.reader;
            reader.Reset();
            reader.Position += 28; //PPtr<GameObject> m_GameObject, m_Enabled, PPtr<MonoScript>
            reader.ReadAlignedString(); //m_Name
            return reader.ReadBytes(reader.ReadInt32());
        }

        private static string ParsePhysics(MonoBehaviour physics)
        {
            try
            {
                var reader = physics.reader;
                reader.Reset();
                reader.Position += 28; //PPtr<GameObject> m_GameObject, m_Enabled, PPtr<MonoScript>
                reader.ReadAlignedString(); //m_Name
                var cubismPhysicsRig = new CubismPhysicsRig(reader);

                var physicsSettings = new CubismPhysics3Json.SerializablePhysicsSettings[cubismPhysicsRig.SubRigs.Length];
                for (int i = 0; i < physicsSettings.Length; i++)
                {
                    var subRigs = cubismPhysicsRig.SubRigs[i];
                    physicsSettings[i] = new CubismPhysics3Json.SerializablePhysicsSettings
                    {
                        Id = $"PhysicsSetting{i + 1}",
                        Input = new CubismPhysics3Json.SerializableInput[subRigs.Input.Length],
                        Output = new CubismPhysics3Json.SerializableOutput[subRigs.Output.Length],
                        Vertices = new CubismPhysics3Json.SerializableVertex[subRigs.Particles.Length],
                        Normalization = new CubismPhysics3Json.SerializableNormalization
                        {
                            Position = new CubismPhysics3Json.SerializableNormalizationValue
                            {
                                Minimum = subRigs.Normalization.Position.Minimum,
                                Default = subRigs.Normalization.Position.Default,
                                Maximum = subRigs.Normalization.Position.Maximum
                            },
                            Angle = new CubismPhysics3Json.SerializableNormalizationValue
                            {
                                Minimum = subRigs.Normalization.Angle.Minimum,
                                Default = subRigs.Normalization.Angle.Default,
                                Maximum = subRigs.Normalization.Angle.Maximum
                            }
                        }
                    };
                    for (int j = 0; j < subRigs.Input.Length; j++)
                    {
                        var input = subRigs.Input[j];
                        physicsSettings[i].Input[j] = new CubismPhysics3Json.SerializableInput
                        {
                            Source = new CubismPhysics3Json.SerializableParameter
                            {
                                Target = "Parameter", //同名GameObject父节点的名称
                                Id = input.SourceId
                            },
                            Weight = input.Weight,
                            Type = Enum.GetName(typeof(CubismPhysicsSourceComponent), input.SourceComponent),
                            Reflect = input.IsInverted
                        };
                    }
                    for (int j = 0; j < subRigs.Output.Length; j++)
                    {
                        var output = subRigs.Output[j];
                        physicsSettings[i].Output[j] = new CubismPhysics3Json.SerializableOutput
                        {
                            Destination = new CubismPhysics3Json.SerializableParameter
                            {
                                Target = "Parameter", //同名GameObject父节点的名称
                                Id = output.DestinationId
                            },
                            VertexIndex = output.ParticleIndex,
                            Scale = output.AngleScale,
                            Weight = output.Weight,
                            Type = Enum.GetName(typeof(CubismPhysicsSourceComponent), output.SourceComponent),
                            Reflect = output.IsInverted
                        };
                    }
                    for (int j = 0; j < subRigs.Particles.Length; j++)
                    {
                        var particles = subRigs.Particles[j];
                        physicsSettings[i].Vertices[j] = new CubismPhysics3Json.SerializableVertex
                        {
                            Position = new CubismPhysics3Json.SerializableVector2
                            {
                                X = particles.InitialPosition.X,
                                Y = particles.InitialPosition.Y
                            },
                            Mobility = particles.Mobility,
                            Delay = particles.Delay,
                            Acceleration = particles.Acceleration,
                            Radius = particles.Radius
                        };
                    }
                }
                var physicsDictionary = new CubismPhysics3Json.SerializablePhysicsDictionary[physicsSettings.Length];
                for (int i = 0; i < physicsSettings.Length; i++)
                {
                    physicsDictionary[i] = new CubismPhysics3Json.SerializablePhysicsDictionary
                    {
                        Id = $"PhysicsSetting{i + 1}",
                        Name = $"Dummy{i + 1}"
                    };
                }
                var physicsJson = new CubismPhysics3Json
                {
                    Version = 3,
                    Meta = new CubismPhysics3Json.SerializableMeta
                    {
                        PhysicsSettingCount = cubismPhysicsRig.SubRigs.Length,
                        TotalInputCount = cubismPhysicsRig.SubRigs.Sum(x => x.Input.Length),
                        TotalOutputCount = cubismPhysicsRig.SubRigs.Sum(x => x.Output.Length),
                        VertexCount = cubismPhysicsRig.SubRigs.Sum(x => x.Particles.Length),
                        EffectiveForces = new CubismPhysics3Json.SerializableEffectiveForces
                        {
                            Gravity = new CubismPhysics3Json.SerializableVector2
                            {
                                X = 0,
                                Y = -1
                            },
                            Wind = new CubismPhysics3Json.SerializableVector2
                            {
                                X = 0,
                                Y = 0
                            }
                        },
                        PhysicsDictionary = physicsDictionary
                    },
                    PhysicsSettings = physicsSettings
                };
                return JsonConvert.SerializeObject(physicsJson, Formatting.Indented, new MyJsonConverter2());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while exporting physics with name: {physics.m_Name}\nReason: {ex}");
            }

            return string.Empty;
        }
    }
}