using System;
using System.Windows.Forms;

namespace AnimeStudio.GUI
{
    internal class BuildMapDialog : Form
    {
        public ExportListType SelectedFormat { get; private set; } = ExportListType.MessagePack;
        public bool IncludeAssetMap { get; private set; } = true;
        public bool IncludeCabMap { get; private set; } = true;

        private readonly RadioButton _rbMsgpack;
        private readonly RadioButton _rbJson;
        private readonly RadioButton _rbXml;
        private readonly CheckBox _cbAsset;
        private readonly CheckBox _cbCab;
        private readonly Button _btnBuild;

        public BuildMapDialog()
        {
            Text = "Build Map";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(300, 190);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new System.Drawing.Font("Segoe UI", 9f);

            var gbFormat = new GroupBox { Text = "Format", Left = 12, Top = 8, Width = 276, Height = 76 };
            _rbMsgpack = new RadioButton { Text = "MessagePack (.map)", Left = 8, Top = 20, Width = 170, Checked = true, AutoSize = true };
            _rbJson    = new RadioButton { Text = "JSON (.json)",       Left = 8, Top = 44, Width = 120, AutoSize = true };
            _rbXml     = new RadioButton { Text = "XML (.xml)",         Left = 148, Top = 44, Width = 100, AutoSize = true };
            gbFormat.Controls.AddRange(new Control[] { _rbMsgpack, _rbJson, _rbXml });

            var gbContent = new GroupBox { Text = "Content", Left = 12, Top = 92, Width = 276, Height = 54 };
            _cbAsset = new CheckBox { Text = "Asset Map", Left = 8,   Top = 20, Width = 110, Checked = true, AutoSize = true };
            _cbCab   = new CheckBox { Text = "CAB Map",   Left = 148, Top = 20, Width = 100, Checked = true, AutoSize = true };
            _cbAsset.CheckedChanged += ValidateContent;
            _cbCab.CheckedChanged   += ValidateContent;
            gbContent.Controls.AddRange(new Control[] { _cbAsset, _cbCab });

            _btnBuild = new Button { Text = "Build", Left = 108, Top = 156, Width = 80, Height = 26 };
            _btnBuild.Click += BtnBuild_Click;
            var btnCancel = new Button { Text = "Cancel", Left = 200, Top = 156, Width = 80, Height = 26, DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[] { gbFormat, gbContent, _btnBuild, btnCancel });
            AcceptButton = _btnBuild;
            CancelButton = btnCancel;
        }

        private void BtnBuild_Click(object sender, EventArgs e)
        {
            SelectedFormat = _rbMsgpack.Checked ? ExportListType.MessagePack
                           : _rbJson.Checked    ? ExportListType.JSON
                           :                      ExportListType.XML;
            IncludeAssetMap = _cbAsset.Checked;
            IncludeCabMap   = _cbCab.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ValidateContent(object sender, EventArgs e)
        {
            _btnBuild.Enabled = _cbAsset.Checked || _cbCab.Checked;
        }
    }
}
