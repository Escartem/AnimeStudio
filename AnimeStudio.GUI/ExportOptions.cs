using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AnimeStudio.GUI.Core;

namespace AnimeStudio.GUI
{
    public partial class ExportOptions : Form
    {
        public bool Resetted { get; private set; }
        private readonly StudioSession session;
        private readonly Dictionary<ClassIDType, (bool, bool)> types;
        private readonly Dictionary<string, (bool, int)> uvs;
        private readonly Dictionary<string, int> texs;

        public ExportOptions(StudioSession session)
        {
            InitializeComponent();
            this.session = session;
            var settings = session.Settings;
            assetGroupOptions.SelectedIndex = (int)settings.AssetGroupOption;
            restoreExtensionName.Checked = settings.RestoreExtensionName;
            converttexture.Checked = settings.ConvertTexture;
            enableHDR.Checked = settings.EnableHDR;
            convertAudio.Checked = settings.ConvertAudio;
            foreach (var radio in panel1.Controls.OfType<RadioButton>())
                radio.Checked = radio.Text == settings.ConvertType.ToString();
            openAfterExport.Checked = settings.OpenAfterExport;
            eulerFilter.Checked = settings.EulerFilter;
            filterPrecision.Value = settings.FilterPrecision;
            exportAllNodes.Checked = settings.ExportAllNodes;
            exportSkins.Checked = settings.ExportSkins;
            exportMaterials.Checked = settings.ExportMaterials;
            exportAnimations.Checked = settings.ExportAnimations;
            exportBlendShape.Checked = settings.ExportBlendShape;
            castToBone.Checked = settings.CastToBone;
            boneSize.Value = settings.BoneSize;
            scaleFactor.Value = settings.ScaleFactor;
            fbxVersion.SelectedIndex = settings.FbxVersion;
            fbxFormat.SelectedIndex = settings.FbxFormat;
            collectAnimations.Checked = settings.CollectAnimations;
            encrypted.Checked = settings.Encrypted;
            key.Value = settings.Key;
            minimalAssetMap.Checked = settings.MinimalAssetMap;
            types = new Dictionary<ClassIDType, (bool, bool)>(settings.Types);
            uvs = new Dictionary<string, (bool, int)>(settings.Uvs);
            texs = new Dictionary<string, int>(settings.Texs);

            texTypeComboBox.SelectedIndex = 0;
            if (texs.Count > 0)
            {
                texNameComboBox.Items.AddRange(texs.Keys.ToArray());
                texNameComboBox.SelectedIndex = 0;
                texTypeComboBox.SelectedIndex = texs.ElementAt(0).Value;
            }

            typesComboBox.SelectedIndex = 0;
            uvsComboBox.SelectedIndex = 0;
        }

        private void OKbutton_Click(object sender, EventArgs e)
        {
            var settings = session.Settings;
            settings.AssetGroupOption = (AssetGroupOption)assetGroupOptions.SelectedIndex;
            settings.RestoreExtensionName = restoreExtensionName.Checked;
            settings.ConvertTexture = converttexture.Checked;
            settings.EnableHDR = enableHDR.Checked;
            settings.ConvertAudio = convertAudio.Checked;
            var format = panel1.Controls.OfType<RadioButton>().FirstOrDefault(x => x.Checked);
            if (format != null)
                settings.ConvertType = Enum.Parse<ImageFormat>(format.Text);
            settings.OpenAfterExport = openAfterExport.Checked;
            settings.EulerFilter = eulerFilter.Checked;
            settings.FilterPrecision = filterPrecision.Value;
            settings.ExportAllNodes = exportAllNodes.Checked;
            settings.ExportSkins = exportSkins.Checked;
            settings.ExportMaterials = exportMaterials.Checked;
            settings.ExportAnimations = exportAnimations.Checked;
            settings.ExportBlendShape = exportBlendShape.Checked;
            settings.CastToBone = castToBone.Checked;
            settings.BoneSize = boneSize.Value;
            settings.ScaleFactor = scaleFactor.Value;
            settings.FbxVersion = fbxVersion.SelectedIndex;
            settings.FbxFormat = fbxFormat.SelectedIndex;
            settings.CollectAnimations = collectAnimations.Checked;
            settings.Encrypted = encrypted.Checked;
            settings.Key = (byte)key.Value;
            settings.MinimalAssetMap = minimalAssetMap.Checked;
            settings.Types = types;
            settings.Uvs = uvs;
            settings.Texs = texs;
            session.ReplaceSettings(settings);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void TypesComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox && types.TryGetValue((ClassIDType)comboBox.SelectedItem, out var param))
            {
                canParseCheckBox.Checked = param.Item1;
                canExportCheckBox.Checked = param.Item2;
            }
        }

        private void CanParseCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is CheckBox checkBox && types.TryGetValue((ClassIDType)typesComboBox.SelectedItem, out var param))
            {
                param.Item1 = checkBox.Checked;
                types[(ClassIDType)typesComboBox.SelectedItem] = (param.Item1, param.Item2);
            }
        }

        private void CanExportCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is CheckBox checkBox && types.TryGetValue((ClassIDType)typesComboBox.SelectedItem, out var param))
            {
                param.Item2 = checkBox.Checked;
                types[(ClassIDType)typesComboBox.SelectedItem] = (param.Item1, param.Item2);
            }
        }

        private void uvsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox && uvs.TryGetValue(comboBox.SelectedItem.ToString(), out var param))
            {
                uvEnabledCheckBox.Checked = param.Item1;
                uvTypesComboBox.SelectedIndex = param.Item2;
            }
        }

        private void uvEnabledCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is CheckBox checkBox && uvs.TryGetValue(uvsComboBox.SelectedItem.ToString(), out var param))
            {
                param.Item1 = checkBox.Checked;
                uvs[uvsComboBox.SelectedItem.ToString()] = (param.Item1, param.Item2);
            }
        }

        private void uvTypesComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox && uvs.TryGetValue(uvsComboBox.SelectedItem.ToString(), out var param))
            {
                param.Item2 = comboBox.SelectedIndex;
                uvs[uvsComboBox.SelectedItem.ToString()] = (param.Item1, param.Item2);
            }
        }

        private void TexNameComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(texNameComboBox.SelectedItem?.ToString()) && texs.TryGetValue(texNameComboBox.SelectedItem?.ToString(), out var type))
            {
                texTypeComboBox.SelectedIndex = type;
            }
        }

        private void AddTexNameButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(texNameComboBox.Text) && !texs.ContainsKey(texNameComboBox.Text))
            {
                texs[texNameComboBox.Text] = texTypeComboBox.SelectedIndex;
                texNameComboBox.Items.Add(texNameComboBox.Text);
                texNameComboBox.SelectedIndex = texNameComboBox.Items.Count - 1;
                ActiveControl = null;
            }
        }

        private void RemoveTexNameButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(texNameComboBox.SelectedItem?.ToString()) && texs.ContainsKey(texNameComboBox.SelectedItem?.ToString()))
            {
                texs.Remove(texNameComboBox.SelectedItem?.ToString());
                texNameComboBox.Items.Remove(texNameComboBox.SelectedItem?.ToString());
                ActiveControl = null;
                if (texNameComboBox.Items.Count > 0)
                {
                    texNameComboBox.SelectedIndex = 0;
                }
                else
                {
                    texNameComboBox.Text = "";
                    texTypeComboBox.SelectedIndex = 0;
                }
            }
        }

        private void TexTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox && !string.IsNullOrEmpty(texNameComboBox.SelectedItem?.ToString()) && texs.ContainsKey(texNameComboBox.SelectedItem?.ToString()))
            {
                texs[texNameComboBox.SelectedItem?.ToString()] = comboBox.SelectedIndex;
            }
        }

        private void TypesComboBox_MouseHover(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var type in types)
            {
                sb.Append($"{type.Key}: {(type.Value.Item1 ? '\x2713' : '\x2717')}, {(type.Value.Item2 ? '\x2713' : '\x2717')}\n");
            }

            toolTip.ToolTipTitle = "Type options status:";
            toolTip.SetToolTip(typesComboBox, sb.ToString());
        }

        private void uvsComboBox_MouseHover(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var uv in uvs)
            {
                sb.Append($"{uv.Key}: {uvTypesComboBox.Items[uv.Value.Item2]}, {(uv.Value.Item1 ? '\x2713' : '\x2717')}\n");
            }

            toolTip.ToolTipTitle = "UVs options status:";
            toolTip.SetToolTip(uvsComboBox, sb.ToString());
        }

        private void TexTypeComboBox_MouseHover(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var tex in texs)
            {
                sb.Append($"{tex.Key}: {texTypeComboBox.Items[tex.Value]}\n");
            }

            toolTip.ToolTipTitle = "Texture options status:";
            toolTip.SetToolTip(texTypeComboBox, sb.ToString());
        }

        private void Key_MouseHover(object sender, EventArgs e)
        {
            toolTip.ToolTipTitle = "Value";
            toolTip.SetToolTip(key, "Key in Hex");
        }

        private void Reset_Click(object sender, EventArgs e)
        {
            session.ReplaceSettings(new StudioSettings());
            Resetted = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
