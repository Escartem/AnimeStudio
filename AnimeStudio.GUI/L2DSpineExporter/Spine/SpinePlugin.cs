using AnimeStudio;
using AnimeStudio.GUI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace L2DSpineExporter
{
    public class Spine
    {

        public static void SaveByMonoBehaviourScript(IEnumerable<AssetItem> monoBehaviours, string folder, bool withSkel, bool withPathID, ToolStripStatusLabel toolStripStatusLabel)
        {
            monoBehaviours.Apply(asset =>
            {
                var monoBehaviour = asset.Asset as MonoBehaviour;
                if (monoBehaviour.m_Script.TryGet(out MonoScript script) && script.Name == "SkeletonDataAsset")
                {
                    if (monoBehaviour.TryGetComponent("skeletonJSON", out TextAsset skel))
                    {
                        #region skelName

                        string skelName;
                        if (skel.Name.EndsWith(".skel") || skel.Name.EndsWith(".json"))
                        {
                            skelName = skel.Name;
                        }
                        else
                        {
                            skelName = Path.GetFileNameWithoutExtension(skel.Name) + ".skel";
                        }

                        #endregion skelName

                        #region subfolder

                        string subfolder = folder;
                        if (withSkel)
                        {
                            subfolder = Path.Combine(subfolder, Path.GetFileNameWithoutExtension(skelName));
                        }
                        if (withPathID)
                        {
                            subfolder = Path.Combine(subfolder, skel.m_PathID.ToString());
                        }
                        Directory.CreateDirectory(subfolder);

                        #endregion subfolder

                        #region saveSkel

                        string skelPath = Path.Combine(subfolder, skelName);
                        File.WriteAllBytes(skelPath, skel.m_Script);
                        Console.WriteLine($"------\nSave {skelPath} successfully.");
                        toolStripStatusLabel.Text = $"Save {skelPath} successfully.";
                        Application.DoEvents();

                        #endregion saveSkel

                        if (monoBehaviour.TryGetComponents("atlasAssets", out List<MonoBehaviour> atalsTargets))
                        {
                            atalsTargets.ForEach(atalsTarget =>
                            {
                                if (atalsTarget.TryGetComponent("atlasFile", out TextAsset atlas))
                                {
                                    #region saveAtlas

                                    string atlasName;
                                    if (atlas.Name.EndsWith(".atlas"))
                                    {
                                        atlasName = atlas.Name;
                                    }
                                    else
                                    {
                                        atlasName = Path.GetFileNameWithoutExtension(atlas.Name) + ".atlas";
                                    }
                                    string atlasPath = Path.Combine(subfolder, atlasName);
                                    File.WriteAllBytes(atlasPath, atlas.m_Script);
                                    Console.WriteLine($"Save {atlasPath} successfully.");
                                    toolStripStatusLabel.Text = $"Save {atlasPath} successfully.";
                                    Application.DoEvents();
                                    #endregion saveAtlas
                                }
                                if (atalsTarget.TryGetComponents("materials", out List<Material> materials))
                                {
                                    var textures = materials.ApplyFunc(material => material.m_SavedProperties.m_TexEnvs[0].Value.m_Texture.TryGet(out Texture2D texture2D) ? texture2D : null, true);
                                    textures.SaveTextures(subfolder,toolStripStatusLabel);
                                    //SaveTextures(textures, subfolder);
                                }
                            });
                        }
                    }
                }
            });
            toolStripStatusLabel.Text = "Done!";
            Console.WriteLine("Done!");
        }

        public static void SaveByMonoBehaviourScript(IEnumerable<AssetItem> monoBehaviours, string folder, bool withSkel, bool withPathID, float scale, int anisoLevel, float posX, float posY, bool withEdgePadding, bool withShader, ToolStripStatusLabel toolStripStatusLabel)
        {
            JObject configOptions = new JObject();
            configOptions["scale_factor"] = scale;
            configOptions["aniso_level"] = anisoLevel;
            configOptions["position_x"] = posX;
            configOptions["position_y"] = posY;
            if (!withEdgePadding)
            {
                configOptions["edge_padding"] = false;
            }
            if (withShader)
            {
                configOptions["shader"] = "Skeleton-Straight-Alpha";
            }
            monoBehaviours.Apply(asset =>
            {
                var monoBehaviour = asset.Asset as MonoBehaviour;
                if (monoBehaviour.m_Script.TryGet(out MonoScript script)&&script.Name == "SkeletonDataAsset")
                {
                    if (monoBehaviour.TryGetComponent("skeletonJSON", out TextAsset skel))
                    {
                        #region skelName

                        string skelName;
                        if (skel.Name.EndsWith(".skel") || skel.Name.EndsWith(".json"))
                        {
                            skelName = skel.Name;
                        }
                        else
                        {
                            skelName = Path.GetFileNameWithoutExtension(skel.Name) + ".skel";
                        }

                        #endregion skelName

                        #region subfolder

                        string subfolder = folder;
                        if (withSkel)
                        {
                            subfolder = Path.Combine(subfolder, Path.GetFileNameWithoutExtension(skelName));
                        }
                        if (withPathID)
                        {
                            subfolder = Path.Combine(subfolder, skel.m_PathID.ToString());
                        }
                        Directory.CreateDirectory(subfolder);

                        #endregion subfolder

                        #region saveSkel

                        string skelPath = Path.Combine(subfolder, skelName);
                        File.WriteAllBytes(skelPath, skel.m_Script);
                        Console.WriteLine($"------\nSave {skelPath} successfully.");
                        toolStripStatusLabel.Text = $"Save {skelPath} successfully.";
                        Application.DoEvents();
                        JObject configRoot = new JObject();
                        configRoot["skeleton"] = skelName;

                        #endregion saveSkel

                        if (monoBehaviour.TryGetComponents("atlasAssets", out List<MonoBehaviour> atalsTargets))
                        {
                            JArray atlases = new JArray();
                            atalsTargets.ForEach(atalsTarget =>
                            {
                                JObject atlasObject = new JObject();
                                if (atalsTarget.TryGetComponent("atlasFile", out TextAsset atlas))
                                {
                                    #region saveAtlas

                                    string atlasName;
                                    if (atlas.Name.EndsWith(".atlas"))
                                    {
                                        atlasName = atlas.Name;
                                    }
                                    else
                                    {
                                        atlasName = Path.GetFileNameWithoutExtension(atlas.Name) + ".atlas";
                                    }
                                    string atlasPath = Path.Combine(subfolder, atlasName);
                                    File.WriteAllBytes(atlasPath, atlas.m_Script);
                                    Console.WriteLine($"Save {atlasPath} successfully.");
                                    toolStripStatusLabel.Text = $"Save {atlasPath} successfully.";
                                    Application.DoEvents();
                                    atlasObject["atlas"] = atlasName;

                                    #endregion saveAtlas
                                }
                                if (atalsTarget.TryGetComponents("materials", out List<Material> materials))
                                {
                                    JArray texNames = new JArray();
                                    JArray textures = new JArray();
                                    
                                    var texture2Ds = materials.ApplyFunc(material => material.m_SavedProperties.m_TexEnvs[0].Value.m_Texture.TryGet(out Texture2D texture2D) ? texture2D : null, true);
                                    foreach (var texture2D in texture2Ds)
                                    {
                                        texture2D.SaveTexture(subfolder,toolStripStatusLabel);
                                        texNames.Add(texture2D.m_Name);
                                        textures.Add(texture2D.m_Name + ".png");
                                    }
                                    atlasObject["tex_names"] = texNames;
                                    atlasObject["textures"] = textures;
                                    atlases.Add(atlasObject);
                                    configRoot["atlases"] = atlases;
                                    configRoot["options"] = configOptions;
                                    string configPath = Path.Combine(subfolder, Path.GetFileNameWithoutExtension(skelName) + ".config.json");
                                    File.WriteAllText(configPath, configRoot.ToString());
                                    Console.WriteLine($"Save {configPath} successfully.");
                                    toolStripStatusLabel.Text = $"Save {configPath} successfully.";
                                    Application.DoEvents();
                                }
                            });
                        }
                    }
                }
            });
            toolStripStatusLabel.Text = "Done!";
            Console.WriteLine("Done!");
        }
    }
}