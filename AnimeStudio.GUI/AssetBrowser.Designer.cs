using System.Configuration;
using System.Windows.Forms;

namespace AnimeStudio.GUI
{
    partial class AssetBrowser
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AssetBrowser));
            tableLayoutPanel2 = new TableLayoutPanel();
            loadAssetMap = new Button();
            clear = new Button();
            loadSelected = new Button();
            exportSelected = new Button();
            relocateSource = new Button();
            assetDataGridView = new DataGridView();
            tableLayoutPanel1 = new TableLayoutPanel();
            tableLayoutPanel3 = new TableLayoutPanel();
            sourceTextBox = new TextBox();
            pathTextBox = new TextBox();
            nameTextBox = new TextBox();
            containerTextBox = new TextBox();
            searchBtn = new Button();
            hashTextBox = new TextBox();
            filterSelectTypesBtn = new Button();
            tableLayoutPanel4 = new TableLayoutPanel();
            loadMapTwoBtn = new Button();
            clearMapTwoBtn = new Button();
            secondMapFilter = new ComboBox();
            tableLayoutPanel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)assetDataGridView).BeginInit();
            tableLayoutPanel1.SuspendLayout();
            tableLayoutPanel3.SuspendLayout();
            tableLayoutPanel4.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel2.ColumnCount = 5;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26.6666718F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13.333333F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26.666666F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle());
            tableLayoutPanel2.Controls.Add(loadAssetMap, 0, 0);
            tableLayoutPanel2.Controls.Add(clear, 1, 0);
            tableLayoutPanel2.Controls.Add(loadSelected, 2, 0);
            tableLayoutPanel2.Controls.Add(exportSelected, 3, 0);
            tableLayoutPanel2.Controls.Add(relocateSource, 4, 0);
            tableLayoutPanel2.Location = new System.Drawing.Point(3, 3);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 1;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel2.Size = new System.Drawing.Size(854, 29);
            tableLayoutPanel2.TabIndex = 3;
            // 
            // loadAssetMap
            // 
            loadAssetMap.Dock = DockStyle.Fill;
            loadAssetMap.Location = new System.Drawing.Point(3, 3);
            loadAssetMap.Name = "loadAssetMap";
            loadAssetMap.Size = new System.Drawing.Size(189, 23);
            loadAssetMap.TabIndex = 0;
            loadAssetMap.Text = "Load AssetMap";
            loadAssetMap.UseVisualStyleBackColor = false;
            loadAssetMap.Click += loadAssetMap_Click;
            // 
            // clear
            // 
            clear.Dock = DockStyle.Fill;
            clear.Location = new System.Drawing.Point(198, 3);
            clear.Name = "clear";
            clear.Size = new System.Drawing.Size(91, 23);
            clear.TabIndex = 1;
            clear.Text = "Clear";
            clear.UseVisualStyleBackColor = false;
            clear.Click += clear_Click;
            // 
            // loadSelected
            // 
            loadSelected.Dock = DockStyle.Fill;
            loadSelected.Enabled = false;
            loadSelected.Location = new System.Drawing.Point(295, 3);
            loadSelected.Name = "loadSelected";
            loadSelected.Size = new System.Drawing.Size(189, 23);
            loadSelected.TabIndex = 2;
            loadSelected.Text = "Load Selected";
            loadSelected.UseVisualStyleBackColor = false;
            loadSelected.Click += loadSelected_Click;
            // 
            // exportSelected
            // 
            exportSelected.Dock = DockStyle.Fill;
            exportSelected.Enabled = false;
            exportSelected.Location = new System.Drawing.Point(490, 3);
            exportSelected.Name = "exportSelected";
            exportSelected.Size = new System.Drawing.Size(238, 23);
            exportSelected.TabIndex = 3;
            exportSelected.Text = "Export Selected";
            exportSelected.UseVisualStyleBackColor = false;
            exportSelected.Click += exportSelected_Click;
            // 
            // relocateSource
            // 
            relocateSource.Enabled = false;
            relocateSource.Location = new System.Drawing.Point(734, 3);
            relocateSource.Name = "relocateSource";
            relocateSource.Size = new System.Drawing.Size(114, 23);
            relocateSource.TabIndex = 4;
            relocateSource.Text = "Relocate source";
            relocateSource.UseVisualStyleBackColor = false;
            relocateSource.Click += relocateSource_Click;
            // 
            // assetDataGridView
            // 
            assetDataGridView.AllowUserToAddRows = false;
            assetDataGridView.AllowUserToDeleteRows = false;
            assetDataGridView.AllowUserToResizeRows = false;
            assetDataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            assetDataGridView.Dock = DockStyle.Fill;
            assetDataGridView.Location = new System.Drawing.Point(3, 103);
            assetDataGridView.Name = "assetDataGridView";
            assetDataGridView.ReadOnly = true;
            assetDataGridView.RowHeadersVisible = false;
            assetDataGridView.RowHeadersWidth = 51;
            assetDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            assetDataGridView.Size = new System.Drawing.Size(854, 502);
            assetDataGridView.TabIndex = 2;
            assetDataGridView.VirtualMode = true;
            assetDataGridView.CellValueNeeded += AssetDataGridView_CellValueNeeded;
            assetDataGridView.ColumnHeaderMouseClick += AssetListView_ColumnHeaderMouseClick;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(assetDataGridView, 0, 3);
            tableLayoutPanel1.Controls.Add(tableLayoutPanel2, 0, 0);
            tableLayoutPanel1.Controls.Add(tableLayoutPanel3, 0, 1);
            tableLayoutPanel1.Controls.Add(tableLayoutPanel4, 0, 2);
            tableLayoutPanel1.Location = new System.Drawing.Point(12, 12);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 4;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tableLayoutPanel1.Size = new System.Drawing.Size(860, 608);
            tableLayoutPanel1.TabIndex = 3;
            // 
            // tableLayoutPanel3
            // 
            tableLayoutPanel3.ColumnCount = 7;
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.3884859F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.3884888F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.3884888F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.3884888F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.3884888F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.0287771F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.0287771F));
            tableLayoutPanel3.Controls.Add(sourceTextBox, 0, 0);
            tableLayoutPanel3.Controls.Add(pathTextBox, 0, 0);
            tableLayoutPanel3.Controls.Add(nameTextBox, 0, 0);
            tableLayoutPanel3.Controls.Add(containerTextBox, 0, 0);
            tableLayoutPanel3.Controls.Add(searchBtn, 6, 0);
            tableLayoutPanel3.Controls.Add(hashTextBox, 5, 0);
            tableLayoutPanel3.Controls.Add(filterSelectTypesBtn, 4, 0);
            tableLayoutPanel3.Dock = DockStyle.Fill;
            tableLayoutPanel3.Location = new System.Drawing.Point(3, 38);
            tableLayoutPanel3.Name = "tableLayoutPanel3";
            tableLayoutPanel3.RowCount = 1;
            tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel3.Size = new System.Drawing.Size(854, 29);
            tableLayoutPanel3.TabIndex = 4;
            // 
            // sourceTextBox
            // 
            sourceTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            sourceTextBox.Enabled = false;
            sourceTextBox.Location = new System.Drawing.Point(247, 3);
            sourceTextBox.Name = "sourceTextBox";
            sourceTextBox.PlaceholderText = "Source";
            sourceTextBox.Size = new System.Drawing.Size(116, 23);
            sourceTextBox.TabIndex = 6;
            sourceTextBox.KeyPress += SourceTextBox_KeyPress;
            // 
            // pathTextBox
            // 
            pathTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pathTextBox.Enabled = false;
            pathTextBox.Location = new System.Drawing.Point(369, 3);
            pathTextBox.Name = "pathTextBox";
            pathTextBox.PlaceholderText = "PathID";
            pathTextBox.Size = new System.Drawing.Size(116, 23);
            pathTextBox.TabIndex = 7;
            pathTextBox.KeyPress += PathTextBox_KeyPress;
            // 
            // nameTextBox
            // 
            nameTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            nameTextBox.Enabled = false;
            nameTextBox.Location = new System.Drawing.Point(3, 3);
            nameTextBox.Name = "nameTextBox";
            nameTextBox.PlaceholderText = "Name";
            nameTextBox.Size = new System.Drawing.Size(116, 23);
            nameTextBox.TabIndex = 4;
            nameTextBox.KeyPress += NameTextBox_KeyPress;
            // 
            // containerTextBox
            // 
            containerTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            containerTextBox.Enabled = false;
            containerTextBox.Location = new System.Drawing.Point(125, 3);
            containerTextBox.Name = "containerTextBox";
            containerTextBox.PlaceholderText = "Container";
            containerTextBox.Size = new System.Drawing.Size(116, 23);
            containerTextBox.TabIndex = 5;
            containerTextBox.KeyPress += ContainerTextBox_KeyPress;
            // 
            // searchBtn
            // 
            searchBtn.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            searchBtn.Enabled = false;
            searchBtn.Location = new System.Drawing.Point(732, 2);
            searchBtn.Margin = new Padding(3, 2, 3, 2);
            searchBtn.Name = "searchBtn";
            searchBtn.Size = new System.Drawing.Size(119, 25);
            searchBtn.TabIndex = 10;
            searchBtn.Text = "Search";
            searchBtn.UseVisualStyleBackColor = false;
            searchBtn.Click += searchBtn_Click;
            // 
            // hashTextBox
            // 
            hashTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            hashTextBox.Enabled = false;
            hashTextBox.Location = new System.Drawing.Point(613, 3);
            hashTextBox.Name = "hashTextBox";
            hashTextBox.PlaceholderText = "SHA256Hash";
            hashTextBox.Size = new System.Drawing.Size(113, 23);
            hashTextBox.TabIndex = 8;
            hashTextBox.KeyPress += HashTextBox_KeyPress;
            // 
            // filterSelectTypesBtn
            // 
            filterSelectTypesBtn.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            filterSelectTypesBtn.Location = new System.Drawing.Point(490, 1);
            filterSelectTypesBtn.Margin = new Padding(2, 1, 2, 1);
            filterSelectTypesBtn.Name = "filterSelectTypesBtn";
            filterSelectTypesBtn.Size = new System.Drawing.Size(118, 27);
            filterSelectTypesBtn.TabIndex = 11;
            filterSelectTypesBtn.Text = "Select Types";
            filterSelectTypesBtn.UseVisualStyleBackColor = false;
            filterSelectTypesBtn.Click += filterSelectTypesBtn_Click;
            // 
            // tableLayoutPanel4
            // 
            tableLayoutPanel4.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel4.ColumnCount = 3;
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33333F));
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel4.Controls.Add(loadMapTwoBtn, 0, 0);
            tableLayoutPanel4.Controls.Add(clearMapTwoBtn, 1, 0);
            tableLayoutPanel4.Controls.Add(secondMapFilter, 2, 0);
            tableLayoutPanel4.Location = new System.Drawing.Point(3, 72);
            tableLayoutPanel4.Margin = new Padding(3, 2, 3, 2);
            tableLayoutPanel4.Name = "tableLayoutPanel4";
            tableLayoutPanel4.RowCount = 1;
            tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel4.Size = new System.Drawing.Size(854, 26);
            tableLayoutPanel4.TabIndex = 5;
            // 
            // loadMapTwoBtn
            // 
            loadMapTwoBtn.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            loadMapTwoBtn.Enabled = false;
            loadMapTwoBtn.Location = new System.Drawing.Point(3, 2);
            loadMapTwoBtn.Margin = new Padding(3, 2, 3, 2);
            loadMapTwoBtn.Name = "loadMapTwoBtn";
            loadMapTwoBtn.Size = new System.Drawing.Size(278, 22);
            loadMapTwoBtn.TabIndex = 0;
            loadMapTwoBtn.Text = "Load second map";
            loadMapTwoBtn.UseVisualStyleBackColor = true;
            loadMapTwoBtn.Click += loadMapTwoBtn_Click;
            // 
            // clearMapTwoBtn
            // 
            clearMapTwoBtn.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            clearMapTwoBtn.Enabled = false;
            clearMapTwoBtn.Location = new System.Drawing.Point(287, 2);
            clearMapTwoBtn.Margin = new Padding(3, 2, 3, 2);
            clearMapTwoBtn.Name = "clearMapTwoBtn";
            clearMapTwoBtn.Size = new System.Drawing.Size(278, 22);
            clearMapTwoBtn.TabIndex = 1;
            clearMapTwoBtn.Text = "Clear second map";
            clearMapTwoBtn.UseVisualStyleBackColor = true;
            clearMapTwoBtn.Click += clearMapTwoBtn_Click;
            // 
            // secondMapFilter
            // 
            secondMapFilter.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            secondMapFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            secondMapFilter.Enabled = false;
            secondMapFilter.FormattingEnabled = true;
            secondMapFilter.Items.AddRange(new object[] { "Assets Only in First Map", "Assets Only in Second Map", "Assets with Differences" });
            secondMapFilter.Location = new System.Drawing.Point(571, 2);
            secondMapFilter.Margin = new Padding(3, 2, 3, 2);
            secondMapFilter.Name = "secondMapFilter";
            secondMapFilter.Size = new System.Drawing.Size(280, 23);
            secondMapFilter.TabIndex = 0;
            secondMapFilter.SelectedIndexChanged += secondMapFilter_SelectedIndexChanged;
            // 
            // AssetBrowser
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(884, 632);
            Controls.Add(tableLayoutPanel1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "AssetBrowser";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Asset Browser";
            FormClosing += AssetBrowser_FormClosing;
            tableLayoutPanel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)assetDataGridView).EndInit();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel3.ResumeLayout(false);
            tableLayoutPanel3.PerformLayout();
            tableLayoutPanel4.ResumeLayout(false);
            ResumeLayout(false);
        }



        #endregion

        private TableLayoutPanel tableLayoutPanel2;
        private Button loadAssetMap;
        private Button clear;
        private Button loadSelected;
        private Button exportSelected;
        private DataGridView assetDataGridView;
        private TableLayoutPanel tableLayoutPanel1;
        private TableLayoutPanel tableLayoutPanel3;
        private TextBox sourceTextBox;
        private TextBox pathTextBox;
        private TextBox nameTextBox;
        private TextBox containerTextBox;
        private TextBox hashTextBox;
        private Button relocateSource;
        private Button searchBtn;
        private TableLayoutPanel tableLayoutPanel4;
        private Button loadMapTwoBtn;
        private Button clearMapTwoBtn;
        private ComboBox secondMapFilter;
        private Button filterSelectTypesBtn;
    }
}