using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AnimeStudio.GUI
{
    public partial class ExportOptions : Form
    {
        public bool Resetted = false;
        private Dictionary<ClassIDType, (bool, bool)> types = new Dictionary<ClassIDType, (bool, bool)>();
        private Dictionary<string, (bool, int)> uvs = new Dictionary<string, (bool, int)>();
        private Dictionary<string, int> texs = new Dictionary<string, int>();
        public ExportOptions()
        {
            InitializeComponent();
            assetGroupOptions.SelectedIndex = Properties.Settings.Default.assetGroupOption;
            restoreExtensionName.Checked = Properties.Settings.Default.restoreExtensionName;
            converttexture.Checked = Properties.Settings.Default.convertTexture;
            enableHDR.Checked = Properties.Settings.Default.enableHDR;
            convertAudio.Checked = Properties.Settings.Default.convertAudio;
            var str = Properties.Settings.Default.convertType.ToString();
            foreach (Control c in panel1.Controls)
            {
                if (c.Text == str)
                {
                    ((RadioButton)c).Checked = true;
                    break;
                }
            }
            openAfterExport.Checked = Properties.Settings.Default.openAfterExport;
            eulerFilter.Checked = Properties.Settings.Default.eulerFilter;
            filterPrecision.Value = Properties.Settings.Default.filterPrecision;
            exportAllNodes.Checked = Properties.Settings.Default.exportAllNodes;
            exportSkins.Checked = Properties.Settings.Default.exportSkins;
            exportMaterials.Checked = Properties.Settings.Default.exportMaterials;
            exportAnimations.Checked = Properties.Settings.Default.exportAnimations;
            exportBlendShape.Checked = Properties.Settings.Default.exportBlendShape;
            castToBone.Checked = Properties.Settings.Default.castToBone;
            boneSize.Value = Properties.Settings.Default.boneSize;
            scaleFactor.Value = Properties.Settings.Default.scaleFactor;
            fbxVersion.SelectedIndex = Properties.Settings.Default.fbxVersion;
            fbxFormat.SelectedIndex = Properties.Settings.Default.fbxFormat;
            collectAnimations.Checked = Properties.Settings.Default.collectAnimations;
            encrypted.Checked = Properties.Settings.Default.encrypted;
            key.Value = Properties.Settings.Default.key;
            minimalAssetMap.Checked = Properties.Settings.Default.minimalAssetMap;
            types = JsonConvert.DeserializeObject<Dictionary<ClassIDType, (bool, bool)>>(Properties.Settings.Default.types);
            uvs = JsonConvert.DeserializeObject<Dictionary<string, (bool, int)>>(Properties.Settings.Default.uvs);

            texTypeComboBox.SelectedIndex = 0;
            
            if (!string.IsNullOrEmpty(Properties.Settings.Default.texs))
            {
                texs = JsonConvert.DeserializeObject<Dictionary<string, int>>(Properties.Settings.Default.texs);
                if (texs.Count > 0 )
                {
                    texNameComboBox.Items.AddRange(texs.Keys.ToArray());
                    texNameComboBox.SelectedIndex = 0;
                    texTypeComboBox.SelectedIndex = texs.ElementAt(0).Value;
                }
            }

            typesComboBox.SelectedIndex = 0;
            uvsComboBox.SelectedIndex = 0;
        }

        private void OKbutton_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.assetGroupOption = assetGroupOptions.SelectedIndex;
            Properties.Settings.Default.restoreExtensionName = restoreExtensionName.Checked;
            Properties.Settings.Default.convertTexture = converttexture.Checked;
            Properties.Settings.Default.enableHDR = enableHDR.Checked;
            Properties.Settings.Default.convertAudio = convertAudio.Checked;
            foreach (Control c in panel1.Controls)
            {
                if (((RadioButton)c).Checked)
                {
                    Properties.Settings.Default.convertType = (ImageFormat)Enum.Parse(typeof(ImageFormat), c.Text);
                    break;
                }
            }
            Properties.Settings.Default.openAfterExport = openAfterExport.Checked;
            Properties.Settings.Default.eulerFilter = eulerFilter.Checked;
            Properties.Settings.Default.filterPrecision = filterPrecision.Value;
            Properties.Settings.Default.exportAllNodes = exportAllNodes.Checked;
            Properties.Settings.Default.exportSkins = exportSkins.Checked;
            Properties.Settings.Default.exportMaterials = exportMaterials.Checked;
            Properties.Settings.Default.exportAnimations = exportAnimations.Checked;
            Properties.Settings.Default.exportBlendShape = exportBlendShape.Checked;
            Properties.Settings.Default.castToBone = castToBone.Checked;
            Properties.Settings.Default.boneSize = boneSize.Value;
            Properties.Settings.Default.scaleFactor = scaleFactor.Value;
            Properties.Settings.Default.fbxVersion = fbxVersion.SelectedIndex;
            Properties.Settings.Default.fbxFormat = fbxFormat.SelectedIndex;
            Properties.Settings.Default.collectAnimations = collectAnimations.Checked;
            Properties.Settings.Default.encrypted = encrypted.Checked;
            Properties.Settings.Default.key = (byte)key.Value;
            Properties.Settings.Default.minimalAssetMap = minimalAssetMap.Checked;
            Properties.Settings.Default.types = JsonConvert.SerializeObject(types);
            Properties.Settings.Default.uvs = JsonConvert.SerializeObject(uvs);
            Properties.Settings.Default.texs = JsonConvert.SerializeObject(texs);
            Properties.Settings.Default.Save();
            MiHoYoBinData.Key = (byte)key.Value;
            MiHoYoBinData.Encrypted = encrypted.Checked;
            AssetsHelper.Minimal = Properties.Settings.Default.minimalAssetMap;
            TypeFlags.SetTypes(types);
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
            foreach(SettingsProperty settingsProperty in Properties.Settings.Default.Properties)
            {
                Properties.Settings.Default[settingsProperty.Name] = TypeDescriptor.GetConverter(settingsProperty.PropertyType).ConvertFrom(settingsProperty.DefaultValue);
            }
            Properties.Settings.Default.Save();

            DialogResult = DialogResult.Cancel;
            Resetted = true;
            Close();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
