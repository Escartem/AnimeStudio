using System;
using OpenTK;
using OpenTK.GLControl;

namespace AnimeStudio.GUI
{
    partial class MainForm
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            menuStrip1 = new System.Windows.Forms.MenuStrip();
            fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            loadFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            loadFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            extractFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            extractFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            resetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            abortStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            gameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            gameSelect = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem14 = new System.Windows.Forms.ToolStripMenuItem();
            specifyUnityVersion = new System.Windows.Forms.ToolStripTextBox();
            editUnityCNKeysToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator13 = new System.Windows.Forms.ToolStripSeparator();
            assetLoadingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            enableResolveDependencies = new System.Windows.Forms.ToolStripMenuItem();
            allowDuplicates = new System.Windows.Forms.ToolStripMenuItem();
            useBundleContainerNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            skipContainer = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator12 = new System.Windows.Forms.ToolStripSeparator();
            generalToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            showExpOpt = new System.Windows.Forms.ToolStripMenuItem();
            appThemeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            specifyTheme = new System.Windows.Forms.ToolStripComboBox();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            miscToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem21 = new System.Windows.Forms.ToolStripMenuItem();
            displayAll = new System.Windows.Forms.ToolStripMenuItem();
            enablePreview = new System.Windows.Forms.ToolStripMenuItem();
            enableModelPreview = new System.Windows.Forms.ToolStripMenuItem();
            modelsOnly = new System.Windows.Forms.ToolStripMenuItem();
            displayInfo = new System.Windows.Forms.ToolStripMenuItem();
            debugToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem15 = new System.Windows.Forms.ToolStripMenuItem();
            exportClassStructuresMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            enableConsole = new System.Windows.Forms.ToolStripMenuItem();
            clearConsoleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            enableFileLogging = new System.Windows.Forms.ToolStripMenuItem();
            loggedEventsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            generalToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportAllAssetsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportSelectedAssetsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportFilteredAssetsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            formatSpecificToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem4 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem5 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem6 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem3 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem7 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem8 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem9 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem16 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem17 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem24 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem25 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            assetsStructureToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem10 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem11 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem12 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem13 = new System.Windows.Forms.ToolStripMenuItem();
            sceneHierarchy = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            toolStripMenuItem30 = new System.Windows.Forms.ToolStripMenuItem();
            modelsIncludeAnimationClips = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem29 = new System.Windows.Forms.ToolStripMenuItem();
            modelsMerge = new System.Windows.Forms.ToolStripMenuItem();
            modelsObjectsExportAll = new System.Windows.Forms.ToolStripMenuItem();
            modelsObjectsExportSelected = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem28 = new System.Windows.Forms.ToolStripMenuItem();
            modelsNodesExportSelected = new System.Windows.Forms.ToolStripMenuItem();
            filterTypeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            allToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            miscToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            assetMapToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            buildAssetMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            assetMapTypeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            assetBrowserToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator8 = new System.Windows.Forms.ToolStripSeparator();
            cABMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            MapNameComboBox = new System.Windows.Forms.ToolStripComboBox();
            buildMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            loadCABMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            clearMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator15 = new System.Windows.Forms.ToolStripSeparator();
            assetMapCABMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripTextBox1 = new System.Windows.Forms.ToolStripTextBox();
            buildBothToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator14 = new System.Windows.Forms.ToolStripSeparator();
            toolStripMenuItem20 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem19 = new System.Windows.Forms.ToolStripMenuItem();
            specifyAIVersion = new System.Windows.Forms.ToolStripComboBox();
            loadAIToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            assetHelpersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            MapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            assetMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            splitContainer1 = new System.Windows.Forms.SplitContainer();
            tabControl1 = new System.Windows.Forms.TabControl();
            tabPage1 = new System.Windows.Forms.TabPage();
            sceneTreeView = new GOHierarchy();
            treeSearch = new System.Windows.Forms.TextBox();
            tabPage2 = new System.Windows.Forms.TabPage();
            assetListView = new System.Windows.Forms.ListView();
            columnHeaderName = new System.Windows.Forms.ColumnHeader();
            columnHeaderContainer = new System.Windows.Forms.ColumnHeader();
            columnHeaderType = new System.Windows.Forms.ColumnHeader();
            columnHeaderPathID = new System.Windows.Forms.ColumnHeader();
            columnHeaderSize = new System.Windows.Forms.ColumnHeader();
            columnHeaderSHA256 = new System.Windows.Forms.ColumnHeader();
            listSearch = new System.Windows.Forms.TextBox();
            tabPage3 = new System.Windows.Forms.TabPage();
            classesListView = new System.Windows.Forms.ListView();
            columnHeader1 = new System.Windows.Forms.ColumnHeader();
            columnHeader2 = new System.Windows.Forms.ColumnHeader();
            progressbarPanel = new System.Windows.Forms.Panel();
            progressBar1 = new System.Windows.Forms.ProgressBar();
            tabControl2 = new System.Windows.Forms.TabControl();
            tabPage4 = new System.Windows.Forms.TabPage();
            previewPanel = new System.Windows.Forms.Panel();
            assetInfoLabel = new System.Windows.Forms.Label();
            FMODpanel = new System.Windows.Forms.Panel();
            FMODcopyright = new System.Windows.Forms.Label();
            FMODinfoLabel = new System.Windows.Forms.Label();
            FMODtimerLabel = new System.Windows.Forms.Label();
            FMODstatusLabel = new System.Windows.Forms.Label();
            FMODprogressBar = new System.Windows.Forms.TrackBar();
            FMODvolumeBar = new System.Windows.Forms.TrackBar();
            FMODloopButton = new System.Windows.Forms.CheckBox();
            FMODstopButton = new System.Windows.Forms.Button();
            FMODpauseButton = new System.Windows.Forms.Button();
            FMODplayButton = new System.Windows.Forms.Button();
            fontPreviewBox = new System.Windows.Forms.RichTextBox();
            glControl = new GLControl();
            textPreviewBox = new System.Windows.Forms.TextBox();
            classTextBox = new System.Windows.Forms.TextBox();
            tabPage5 = new System.Windows.Forms.TabPage();
            dumpTextBox = new System.Windows.Forms.TextBox();
            statusStrip1 = new System.Windows.Forms.StatusStrip();
            toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            timer = new System.Windows.Forms.Timer(components);
            openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(components);
            copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportSelectedAssetsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportAnimatorwithselectedAnimationClipMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            goToSceneHierarchyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            showOriginalFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            tabPage2.SuspendLayout();
            tabPage3.SuspendLayout();
            progressbarPanel.SuspendLayout();
            tabControl2.SuspendLayout();
            tabPage4.SuspendLayout();
            previewPanel.SuspendLayout();
            FMODpanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)FMODprogressBar).BeginInit();
            ((System.ComponentModel.ISupportInitialize)FMODvolumeBar).BeginInit();
            tabPage5.SuspendLayout();
            statusStrip1.SuspendLayout();
            contextMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.BackColor = System.Drawing.SystemColors.MenuBar;
            menuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem, optionsToolStripMenuItem, exportToolStripMenuItem, filterTypeToolStripMenuItem, miscToolStripMenuItem, aboutToolStripMenuItem });
            menuStrip1.Location = new System.Drawing.Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new System.Drawing.Size(1582, 42);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { loadFileToolStripMenuItem, loadFolderToolStripMenuItem, toolStripMenuItem1, extractFileToolStripMenuItem, extractFolderToolStripMenuItem, toolStripSeparator6, resetToolStripMenuItem, abortStripMenuItem, toolStripSeparator4, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new System.Drawing.Size(71, 38);
            fileToolStripMenuItem.Text = "File";
            // 
            // loadFileToolStripMenuItem
            // 
            loadFileToolStripMenuItem.Name = "loadFileToolStripMenuItem";
            loadFileToolStripMenuItem.Size = new System.Drawing.Size(287, 44);
            loadFileToolStripMenuItem.Text = "Load file";
            loadFileToolStripMenuItem.Click += loadFile_Click;
            // 
            // loadFolderToolStripMenuItem
            // 
            loadFolderToolStripMenuItem.Name = "loadFolderToolStripMenuItem";
            loadFolderToolStripMenuItem.Size = new System.Drawing.Size(287, 44);
            loadFolderToolStripMenuItem.Text = "Load folder";
            loadFolderToolStripMenuItem.Click += loadFolder_Click;
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new System.Drawing.Size(284, 6);
            // 
            // extractFileToolStripMenuItem
            // 
            extractFileToolStripMenuItem.Name = "extractFileToolStripMenuItem";
            extractFileToolStripMenuItem.Size = new System.Drawing.Size(287, 44);
            extractFileToolStripMenuItem.Text = "Extract file";
            extractFileToolStripMenuItem.Click += extractFileToolStripMenuItem_Click;
            // 
            // extractFolderToolStripMenuItem
            // 
            extractFolderToolStripMenuItem.Name = "extractFolderToolStripMenuItem";
            extractFolderToolStripMenuItem.Size = new System.Drawing.Size(287, 44);
            extractFolderToolStripMenuItem.Text = "Extract folder";
            extractFolderToolStripMenuItem.Click += extractFolderToolStripMenuItem_Click;
            // 
            // toolStripSeparator6
            // 
            toolStripSeparator6.Name = "toolStripSeparator6";
            toolStripSeparator6.Size = new System.Drawing.Size(284, 6);
            // 
            // resetToolStripMenuItem
            // 
            resetToolStripMenuItem.Name = "resetToolStripMenuItem";
            resetToolStripMenuItem.Size = new System.Drawing.Size(287, 44);
            resetToolStripMenuItem.Text = "Reset";
            resetToolStripMenuItem.Click += resetToolStripMenuItem_Click;
            // 
            // abortStripMenuItem
            // 
            abortStripMenuItem.Name = "abortStripMenuItem";
            abortStripMenuItem.Size = new System.Drawing.Size(287, 44);
            abortStripMenuItem.Text = "Abort";
            abortStripMenuItem.Click += abortStripMenuItem_Click;
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            toolStripSeparator4.Size = new System.Drawing.Size(284, 6);
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new System.Drawing.Size(287, 44);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // optionsToolStripMenuItem
            // 
            optionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { gameToolStripMenuItem, gameSelect, toolStripMenuItem14, editUnityCNKeysToolStripMenuItem, toolStripSeparator13, assetLoadingToolStripMenuItem, enableResolveDependencies, allowDuplicates, useBundleContainerNameToolStripMenuItem, skipContainer, toolStripSeparator12, generalToolStripMenuItem1, showExpOpt, appThemeToolStripMenuItem, toolStripSeparator1, miscToolStripMenuItem1, toolStripMenuItem21, debugToolStripMenuItem });
            optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            optionsToolStripMenuItem.Size = new System.Drawing.Size(118, 38);
            optionsToolStripMenuItem.Text = "Options";
            // 
            // gameToolStripMenuItem
            // 
            gameToolStripMenuItem.Enabled = false;
            gameToolStripMenuItem.Name = "gameToolStripMenuItem";
            gameToolStripMenuItem.Size = new System.Drawing.Size(442, 44);
            gameToolStripMenuItem.Text = "Game";
            // 
            // gameSelect
            // 
            gameSelect.Name = "gameSelect";
            gameSelect.Size = new System.Drawing.Size(442, 44);
            gameSelect.Text = "Select Game";
            gameSelect.Click += gameSelectToolStripMenuItem_Click;
            // 
            // toolStripMenuItem14
            // 
            toolStripMenuItem14.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { specifyUnityVersion });
            toolStripMenuItem14.Name = "toolStripMenuItem14";
            toolStripMenuItem14.Size = new System.Drawing.Size(442, 44);
            toolStripMenuItem14.Text = "Specify Unity version";
            // 
            // specifyUnityVersion
            // 
            specifyUnityVersion.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            specifyUnityVersion.Name = "specifyUnityVersion";
            specifyUnityVersion.Size = new System.Drawing.Size(100, 38);
            // 
            // editUnityCNKeysToolStripMenuItem
            // 
            editUnityCNKeysToolStripMenuItem.Name = "editUnityCNKeysToolStripMenuItem";
            editUnityCNKeysToolStripMenuItem.Size = new System.Drawing.Size(442, 44);
            editUnityCNKeysToolStripMenuItem.Text = "Edit UnityCN Keys";
            editUnityCNKeysToolStripMenuItem.Click += editUnityCNKeysToolStripMenuItem_Click;
            // 
            // toolStripSeparator13
            // 
            toolStripSeparator13.Name = "toolStripSeparator13";
            toolStripSeparator13.Size = new System.Drawing.Size(439, 6);
            // 
            // assetLoadingToolStripMenuItem
            // 
            assetLoadingToolStripMenuItem.Enabled = false;
            assetLoadingToolStripMenuItem.Name = "assetLoadingToolStripMenuItem";
            assetLoadingToolStripMenuItem.Size = new System.Drawing.Size(442, 44);
            assetLoadingToolStripMenuItem.Text = "Asset loading";
            // 
            // enableResolveDependencies
            // 
            enableResolveDependencies.Checked = true;
            enableResolveDependencies.CheckOnClick = true;
            enableResolveDependencies.CheckState = System.Windows.Forms.CheckState.Checked;
            enableResolveDependencies.Name = "enableResolveDependencies";
            enableResolveDependencies.Size = new System.Drawing.Size(442, 44);
            enableResolveDependencies.Text = "Resolve dependencies";
            enableResolveDependencies.ToolTipText = "Toggle the behaviour of loading assets.\r\nDisable to load file(s) without its dependencies.";
            enableResolveDependencies.CheckedChanged += enableResolveDependencies_CheckedChanged;
            // 
            // allowDuplicates
            // 
            allowDuplicates.CheckOnClick = true;
            allowDuplicates.Name = "allowDuplicates";
            allowDuplicates.Size = new System.Drawing.Size(442, 44);
            allowDuplicates.Text = "Allow duplicates";
            allowDuplicates.ToolTipText = "Toggle the behaviour of exporting assets.\r\nEnable to allow assets with duplicate names to be exported.";
            allowDuplicates.CheckedChanged += allowDuplicates_CheckedChanged;
            // 
            // useBundleContainerNameToolStripMenuItem
            // 
            useBundleContainerNameToolStripMenuItem.CheckOnClick = true;
            useBundleContainerNameToolStripMenuItem.Name = "useBundleContainerNameToolStripMenuItem";
            useBundleContainerNameToolStripMenuItem.Size = new System.Drawing.Size(442, 44);
            useBundleContainerNameToolStripMenuItem.Text = "Use bundle container name";
            useBundleContainerNameToolStripMenuItem.CheckedChanged += UseBundleContainerNameToolStripMenuItem_CheckedChanged;
            // 
            // skipContainer
            // 
            skipContainer.CheckOnClick = true;
            skipContainer.Name = "skipContainer";
            skipContainer.Size = new System.Drawing.Size(442, 44);
            skipContainer.Text = "Skip container recovery";
            skipContainer.ToolTipText = "Skips the container recovery step.\nImproves loading when dealing with a large number of files.";
            skipContainer.CheckedChanged += skipContainer_CheckedChanged;
            // 
            // toolStripSeparator12
            // 
            toolStripSeparator12.Name = "toolStripSeparator12";
            toolStripSeparator12.Size = new System.Drawing.Size(439, 6);
            // 
            // generalToolStripMenuItem1
            // 
            generalToolStripMenuItem1.Enabled = false;
            generalToolStripMenuItem1.Name = "generalToolStripMenuItem1";
            generalToolStripMenuItem1.Size = new System.Drawing.Size(442, 44);
            generalToolStripMenuItem1.Text = "General";
            // 
            // showExpOpt
            // 
            showExpOpt.Name = "showExpOpt";
            showExpOpt.Size = new System.Drawing.Size(442, 44);
            showExpOpt.Text = "Export options";
            showExpOpt.Click += showExpOpt_Click;
            // 
            // appThemeToolStripMenuItem
            // 
            appThemeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { specifyTheme });
            appThemeToolStripMenuItem.Name = "appThemeToolStripMenuItem";
            appThemeToolStripMenuItem.Size = new System.Drawing.Size(442, 44);
            appThemeToolStripMenuItem.Text = "App Theme";
            // 
            // specifyTheme
            // 
            specifyTheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            specifyTheme.Items.AddRange(new object[] { "System", "Dark", "Light" });
            specifyTheme.Name = "specifyTheme";
            specifyTheme.Size = new System.Drawing.Size(121, 40);
            specifyTheme.SelectedIndexChanged += specifyTheme_SelectedIndexChanged;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(439, 6);
            // 
            // miscToolStripMenuItem1
            // 
            miscToolStripMenuItem1.Enabled = false;
            miscToolStripMenuItem1.Name = "miscToolStripMenuItem1";
            miscToolStripMenuItem1.Size = new System.Drawing.Size(442, 44);
            miscToolStripMenuItem1.Text = "Misc.";
            // 
            // toolStripMenuItem21
            // 
            toolStripMenuItem21.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { displayAll, enablePreview, enableModelPreview, modelsOnly, displayInfo });
            toolStripMenuItem21.Name = "toolStripMenuItem21";
            toolStripMenuItem21.Size = new System.Drawing.Size(442, 44);
            toolStripMenuItem21.Text = "Advanced";
            // 
            // displayAll
            // 
            displayAll.CheckOnClick = true;
            displayAll.Name = "displayAll";
            displayAll.Size = new System.Drawing.Size(416, 44);
            displayAll.Text = "Show hidden assets";
            displayAll.ToolTipText = "Check this option will display all types assets. Not extractable assets can export the RAW file.";
            displayAll.CheckedChanged += displayAll_CheckedChanged;
            // 
            // enablePreview
            // 
            enablePreview.Checked = true;
            enablePreview.CheckOnClick = true;
            enablePreview.CheckState = System.Windows.Forms.CheckState.Checked;
            enablePreview.Name = "enablePreview";
            enablePreview.Size = new System.Drawing.Size(416, 44);
            enablePreview.Text = "Preview enabled";
            enablePreview.ToolTipText = "Toggle the loading and preview of readable assets, such as images, sounds, text, etc.\r\nDisable preview if you have performance or compatibility issues.";
            // 
            // enableModelPreview
            // 
            enableModelPreview.CheckOnClick = true;
            enableModelPreview.Name = "enableModelPreview";
            enableModelPreview.Size = new System.Drawing.Size(416, 44);
            enableModelPreview.Text = "Model preview enabled";
            enableModelPreview.CheckedChanged += enableModelPreview_CheckedChanged;
            // 
            // modelsOnly
            // 
            modelsOnly.CheckOnClick = true;
            modelsOnly.Name = "modelsOnly";
            modelsOnly.Size = new System.Drawing.Size(416, 44);
            modelsOnly.Text = "Filter models only";
            // 
            // displayInfo
            // 
            displayInfo.Checked = true;
            displayInfo.CheckOnClick = true;
            displayInfo.CheckState = System.Windows.Forms.CheckState.Checked;
            displayInfo.Name = "displayInfo";
            displayInfo.Size = new System.Drawing.Size(416, 44);
            displayInfo.Text = "Display asset information";
            displayInfo.ToolTipText = "Toggle the overlay that shows information about each asset, eg. image size, format, audio bitrate, etc.";
            // 
            // debugToolStripMenuItem
            // 
            debugToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripMenuItem15, exportClassStructuresMenuItem, enableConsole, clearConsoleToolStripMenuItem, enableFileLogging, loggedEventsMenuItem });
            debugToolStripMenuItem.Name = "debugToolStripMenuItem";
            debugToolStripMenuItem.Size = new System.Drawing.Size(442, 44);
            debugToolStripMenuItem.Text = "Debug";
            // 
            // toolStripMenuItem15
            // 
            toolStripMenuItem15.Checked = true;
            toolStripMenuItem15.CheckOnClick = true;
            toolStripMenuItem15.CheckState = System.Windows.Forms.CheckState.Checked;
            toolStripMenuItem15.Name = "toolStripMenuItem15";
            toolStripMenuItem15.Size = new System.Drawing.Size(381, 44);
            toolStripMenuItem15.Text = "Show error messages";
            toolStripMenuItem15.Click += toolStripMenuItem15_Click;
            // 
            // exportClassStructuresMenuItem
            // 
            exportClassStructuresMenuItem.Name = "exportClassStructuresMenuItem";
            exportClassStructuresMenuItem.Size = new System.Drawing.Size(381, 44);
            exportClassStructuresMenuItem.Text = "Export class structures";
            exportClassStructuresMenuItem.Click += exportClassStructuresMenuItem_Click;
            // 
            // enableConsole
            // 
            enableConsole.Checked = true;
            enableConsole.CheckOnClick = true;
            enableConsole.CheckState = System.Windows.Forms.CheckState.Checked;
            enableConsole.Name = "enableConsole";
            enableConsole.Size = new System.Drawing.Size(381, 44);
            enableConsole.Text = "Enable Console";
            enableConsole.CheckedChanged += enableConsole_CheckedChanged;
            // 
            // clearConsoleToolStripMenuItem
            // 
            clearConsoleToolStripMenuItem.Name = "clearConsoleToolStripMenuItem";
            clearConsoleToolStripMenuItem.Size = new System.Drawing.Size(381, 44);
            clearConsoleToolStripMenuItem.Text = "Clear Console";
            clearConsoleToolStripMenuItem.Click += clearConsoleToolStripMenuItem_Click;
            // 
            // enableFileLogging
            // 
            enableFileLogging.Checked = true;
            enableFileLogging.CheckOnClick = true;
            enableFileLogging.CheckState = System.Windows.Forms.CheckState.Checked;
            enableFileLogging.Name = "enableFileLogging";
            enableFileLogging.Size = new System.Drawing.Size(381, 44);
            enableFileLogging.Text = "Enable file logging";
            enableFileLogging.CheckedChanged += enableFileLogging_CheckedChanged;
            // 
            // loggedEventsMenuItem
            // 
            loggedEventsMenuItem.Name = "loggedEventsMenuItem";
            loggedEventsMenuItem.Size = new System.Drawing.Size(381, 44);
            loggedEventsMenuItem.Text = "Logged events";
            loggedEventsMenuItem.DropDownClosed += loggedEventsMenuItem_DropDownClosed;
            // 
            // exportToolStripMenuItem
            // 
            exportToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { generalToolStripMenuItem, exportAllAssetsMenuItem, exportSelectedAssetsMenuItem, exportFilteredAssetsMenuItem, toolStripSeparator3, formatSpecificToolStripMenuItem, toolStripMenuItem2, toolStripMenuItem3, toolStripMenuItem16, toolStripSeparator2, assetsStructureToolStripMenuItem, toolStripMenuItem10, sceneHierarchy, toolStripSeparator7, toolStripMenuItem30, modelsIncludeAnimationClips, toolStripMenuItem29, modelsMerge, modelsObjectsExportAll, modelsObjectsExportSelected, toolStripMenuItem28, modelsNodesExportSelected });
            exportToolStripMenuItem.Name = "exportToolStripMenuItem";
            exportToolStripMenuItem.Size = new System.Drawing.Size(101, 38);
            exportToolStripMenuItem.Text = "Export";
            // 
            // generalToolStripMenuItem
            // 
            generalToolStripMenuItem.Enabled = false;
            generalToolStripMenuItem.Name = "generalToolStripMenuItem";
            generalToolStripMenuItem.Size = new System.Drawing.Size(400, 44);
            generalToolStripMenuItem.Text = "General";
            // 
            // exportAllAssetsMenuItem
            // 
            exportAllAssetsMenuItem.Name = "exportAllAssetsMenuItem";
            exportAllAssetsMenuItem.Size = new System.Drawing.Size(400, 44);
            exportAllAssetsMenuItem.Text = "All assets";
            exportAllAssetsMenuItem.Click += exportAllAssetsMenuItem_Click;
            // 
            // exportSelectedAssetsMenuItem
            // 
            exportSelectedAssetsMenuItem.Name = "exportSelectedAssetsMenuItem";
            exportSelectedAssetsMenuItem.Size = new System.Drawing.Size(400, 44);
            exportSelectedAssetsMenuItem.Text = "Selected assets";
            exportSelectedAssetsMenuItem.Click += exportSelectedAssetsMenuItem_Click;
            // 
            // exportFilteredAssetsMenuItem
            // 
            exportFilteredAssetsMenuItem.Name = "exportFilteredAssetsMenuItem";
            exportFilteredAssetsMenuItem.Size = new System.Drawing.Size(400, 44);
            exportFilteredAssetsMenuItem.Text = "Filtered assets";
            exportFilteredAssetsMenuItem.Click += exportFilteredAssetsMenuItem_Click;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new System.Drawing.Size(397, 6);
            // 
            // formatSpecificToolStripMenuItem
            // 
            formatSpecificToolStripMenuItem.Enabled = false;
            formatSpecificToolStripMenuItem.Name = "formatSpecificToolStripMenuItem";
            formatSpecificToolStripMenuItem.Size = new System.Drawing.Size(400, 44);
            formatSpecificToolStripMenuItem.Text = "Format specific";
            // 
            // toolStripMenuItem2
            // 
            toolStripMenuItem2.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripMenuItem4, toolStripMenuItem5, toolStripMenuItem6 });
            toolStripMenuItem2.Name = "toolStripMenuItem2";
            toolStripMenuItem2.Size = new System.Drawing.Size(400, 44);
            toolStripMenuItem2.Text = "Raw";
            // 
            // toolStripMenuItem4
            // 
            toolStripMenuItem4.Name = "toolStripMenuItem4";
            toolStripMenuItem4.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem4.Text = "All assets";
            toolStripMenuItem4.Click += toolStripMenuItem4_Click;
            // 
            // toolStripMenuItem5
            // 
            toolStripMenuItem5.Name = "toolStripMenuItem5";
            toolStripMenuItem5.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem5.Text = "Selected assets";
            toolStripMenuItem5.Click += toolStripMenuItem5_Click;
            // 
            // toolStripMenuItem6
            // 
            toolStripMenuItem6.Name = "toolStripMenuItem6";
            toolStripMenuItem6.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem6.Text = "Filtered assets";
            toolStripMenuItem6.Click += toolStripMenuItem6_Click;
            // 
            // toolStripMenuItem3
            // 
            toolStripMenuItem3.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripMenuItem7, toolStripMenuItem8, toolStripMenuItem9 });
            toolStripMenuItem3.Name = "toolStripMenuItem3";
            toolStripMenuItem3.Size = new System.Drawing.Size(400, 44);
            toolStripMenuItem3.Text = "Dump";
            // 
            // toolStripMenuItem7
            // 
            toolStripMenuItem7.Name = "toolStripMenuItem7";
            toolStripMenuItem7.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem7.Text = "All assets";
            toolStripMenuItem7.Click += toolStripMenuItem7_Click;
            // 
            // toolStripMenuItem8
            // 
            toolStripMenuItem8.Name = "toolStripMenuItem8";
            toolStripMenuItem8.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem8.Text = "Selected assets";
            toolStripMenuItem8.Click += toolStripMenuItem8_Click;
            // 
            // toolStripMenuItem9
            // 
            toolStripMenuItem9.Name = "toolStripMenuItem9";
            toolStripMenuItem9.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem9.Text = "Filtered assets";
            toolStripMenuItem9.Click += toolStripMenuItem9_Click;
            // 
            // toolStripMenuItem16
            // 
            toolStripMenuItem16.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripMenuItem17, toolStripMenuItem24, toolStripMenuItem25 });
            toolStripMenuItem16.Name = "toolStripMenuItem16";
            toolStripMenuItem16.Size = new System.Drawing.Size(400, 44);
            toolStripMenuItem16.Text = "JSON";
            // 
            // toolStripMenuItem17
            // 
            toolStripMenuItem17.Name = "toolStripMenuItem17";
            toolStripMenuItem17.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem17.Text = "All assets";
            toolStripMenuItem17.Click += toolStripMenuItem17_Click;
            // 
            // toolStripMenuItem24
            // 
            toolStripMenuItem24.Name = "toolStripMenuItem24";
            toolStripMenuItem24.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem24.Text = "Selected assets";
            toolStripMenuItem24.Click += toolStripMenuItem24_Click;
            // 
            // toolStripMenuItem25
            // 
            toolStripMenuItem25.Name = "toolStripMenuItem25";
            toolStripMenuItem25.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem25.Text = "Filtered assets";
            toolStripMenuItem25.Click += toolStripMenuItem25_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(397, 6);
            // 
            // assetsStructureToolStripMenuItem
            // 
            assetsStructureToolStripMenuItem.Enabled = false;
            assetsStructureToolStripMenuItem.Name = "assetsStructureToolStripMenuItem";
            assetsStructureToolStripMenuItem.Size = new System.Drawing.Size(400, 44);
            assetsStructureToolStripMenuItem.Text = "Assets structure";
            // 
            // toolStripMenuItem10
            // 
            toolStripMenuItem10.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripMenuItem11, toolStripMenuItem12, toolStripMenuItem13 });
            toolStripMenuItem10.Name = "toolStripMenuItem10";
            toolStripMenuItem10.Size = new System.Drawing.Size(400, 44);
            toolStripMenuItem10.Text = "Asset list to XML";
            // 
            // toolStripMenuItem11
            // 
            toolStripMenuItem11.Name = "toolStripMenuItem11";
            toolStripMenuItem11.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem11.Text = "All assets";
            toolStripMenuItem11.Click += toolStripMenuItem11_Click;
            // 
            // toolStripMenuItem12
            // 
            toolStripMenuItem12.Name = "toolStripMenuItem12";
            toolStripMenuItem12.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem12.Text = "Selected assets";
            toolStripMenuItem12.Click += toolStripMenuItem12_Click;
            // 
            // toolStripMenuItem13
            // 
            toolStripMenuItem13.Name = "toolStripMenuItem13";
            toolStripMenuItem13.Size = new System.Drawing.Size(308, 44);
            toolStripMenuItem13.Text = "Filtered assets";
            toolStripMenuItem13.Click += toolStripMenuItem13_Click;
            // 
            // sceneHierarchy
            // 
            sceneHierarchy.Name = "sceneHierarchy";
            sceneHierarchy.Size = new System.Drawing.Size(400, 44);
            sceneHierarchy.Text = "Scene hierarchy";
            sceneHierarchy.Click += sceneHierarchy_Click;
            // 
            // toolStripSeparator7
            // 
            toolStripSeparator7.Name = "toolStripSeparator7";
            toolStripSeparator7.Size = new System.Drawing.Size(397, 6);
            // 
            // toolStripMenuItem30
            // 
            toolStripMenuItem30.Enabled = false;
            toolStripMenuItem30.Name = "toolStripMenuItem30";
            toolStripMenuItem30.Size = new System.Drawing.Size(400, 44);
            toolStripMenuItem30.Text = "Models";
            // 
            // modelsIncludeAnimationClips
            // 
            modelsIncludeAnimationClips.CheckOnClick = true;
            modelsIncludeAnimationClips.Name = "modelsIncludeAnimationClips";
            modelsIncludeAnimationClips.Size = new System.Drawing.Size(400, 44);
            modelsIncludeAnimationClips.Text = "Include AnimationClips ";
            // 
            // toolStripMenuItem29
            // 
            toolStripMenuItem29.Enabled = false;
            toolStripMenuItem29.Name = "toolStripMenuItem29";
            toolStripMenuItem29.Size = new System.Drawing.Size(400, 44);
            toolStripMenuItem29.Text = "Models - Objects";
            // 
            // modelsMerge
            // 
            modelsMerge.CheckOnClick = true;
            modelsMerge.Name = "modelsMerge";
            modelsMerge.Size = new System.Drawing.Size(400, 44);
            modelsMerge.Text = "Merge (only selected)";
            // 
            // modelsObjectsExportAll
            // 
            modelsObjectsExportAll.Name = "modelsObjectsExportAll";
            modelsObjectsExportAll.Size = new System.Drawing.Size(400, 44);
            modelsObjectsExportAll.Text = "Export all";
            modelsObjectsExportAll.Click += modelsObjectsExportAll_Click;
            // 
            // modelsObjectsExportSelected
            // 
            modelsObjectsExportSelected.Name = "modelsObjectsExportSelected";
            modelsObjectsExportSelected.Size = new System.Drawing.Size(400, 44);
            modelsObjectsExportSelected.Text = "Export selected";
            modelsObjectsExportSelected.Click += modelsObjectsExportSelected_Click;
            // 
            // toolStripMenuItem28
            // 
            toolStripMenuItem28.Enabled = false;
            toolStripMenuItem28.Name = "toolStripMenuItem28";
            toolStripMenuItem28.Size = new System.Drawing.Size(400, 44);
            toolStripMenuItem28.Text = "Models - Nodes";
            // 
            // modelsNodesExportSelected
            // 
            modelsNodesExportSelected.Name = "modelsNodesExportSelected";
            modelsNodesExportSelected.Size = new System.Drawing.Size(400, 44);
            modelsNodesExportSelected.Text = "Export selected";
            modelsNodesExportSelected.Click += modelsNodesExportSelected_Click;
            // 
            // filterTypeToolStripMenuItem
            // 
            filterTypeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { allToolStripMenuItem });
            filterTypeToolStripMenuItem.Name = "filterTypeToolStripMenuItem";
            filterTypeToolStripMenuItem.Size = new System.Drawing.Size(145, 38);
            filterTypeToolStripMenuItem.Text = "Filter Type";
            // 
            // allToolStripMenuItem
            // 
            allToolStripMenuItem.Checked = true;
            allToolStripMenuItem.CheckOnClick = true;
            allToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            allToolStripMenuItem.Name = "allToolStripMenuItem";
            allToolStripMenuItem.Size = new System.Drawing.Size(174, 44);
            allToolStripMenuItem.Text = "All";
            allToolStripMenuItem.Click += typeToolStripMenuItem_Click;
            // 
            // miscToolStripMenuItem
            // 
            miscToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { assetMapToolStripMenuItem1, buildAssetMapToolStripMenuItem, assetMapTypeMenuItem, assetBrowserToolStripMenuItem, toolStripSeparator8, cABMapToolStripMenuItem, MapNameComboBox, buildMapToolStripMenuItem, loadCABMapToolStripMenuItem, clearMapToolStripMenuItem, toolStripSeparator15, assetMapCABMapToolStripMenuItem, toolStripTextBox1, buildBothToolStripMenuItem, toolStripSeparator14, toolStripMenuItem20, toolStripMenuItem19, loadAIToolStripMenuItem });
            miscToolStripMenuItem.Name = "miscToolStripMenuItem";
            miscToolStripMenuItem.Size = new System.Drawing.Size(92, 38);
            miscToolStripMenuItem.Text = "Maps";
            miscToolStripMenuItem.DropDownOpening += miscToolStripMenuItem_DropDownOpening;
            // 
            // assetMapToolStripMenuItem1
            // 
            assetMapToolStripMenuItem1.Enabled = false;
            assetMapToolStripMenuItem1.Name = "assetMapToolStripMenuItem1";
            assetMapToolStripMenuItem1.Size = new System.Drawing.Size(411, 44);
            assetMapToolStripMenuItem1.Text = "Asset Map";
            // 
            // buildAssetMapToolStripMenuItem
            // 
            buildAssetMapToolStripMenuItem.Name = "buildAssetMapToolStripMenuItem";
            buildAssetMapToolStripMenuItem.Size = new System.Drawing.Size(411, 44);
            buildAssetMapToolStripMenuItem.Text = "Build";
            buildAssetMapToolStripMenuItem.Click += buildAssetMapToolStripMenuItem_Click;
            // 
            // assetMapTypeMenuItem
            // 
            assetMapTypeMenuItem.Name = "assetMapTypeMenuItem";
            assetMapTypeMenuItem.Size = new System.Drawing.Size(411, 44);
            assetMapTypeMenuItem.Text = "Map Type";
            assetMapTypeMenuItem.DropDownItemClicked += assetMapTypeMenuItem_DropDownItemClicked;
            // 
            // assetBrowserToolStripMenuItem
            // 
            assetBrowserToolStripMenuItem.Name = "assetBrowserToolStripMenuItem";
            assetBrowserToolStripMenuItem.Size = new System.Drawing.Size(411, 44);
            assetBrowserToolStripMenuItem.Text = "Open Asset Browser";
            assetBrowserToolStripMenuItem.Click += loadAssetMapToolStripMenuItem_Click;
            // 
            // toolStripSeparator8
            // 
            toolStripSeparator8.Name = "toolStripSeparator8";
            toolStripSeparator8.Size = new System.Drawing.Size(408, 6);
            // 
            // cABMapToolStripMenuItem
            // 
            cABMapToolStripMenuItem.Enabled = false;
            cABMapToolStripMenuItem.Name = "cABMapToolStripMenuItem";
            cABMapToolStripMenuItem.Size = new System.Drawing.Size(411, 44);
            cABMapToolStripMenuItem.Text = "CAB Map";
            // 
            // MapNameComboBox
            // 
            MapNameComboBox.Name = "MapNameComboBox";
            MapNameComboBox.Size = new System.Drawing.Size(121, 40);
            MapNameComboBox.ToolTipText = "Enter name of Map here";
            // 
            // buildMapToolStripMenuItem
            // 
            buildMapToolStripMenuItem.Name = "buildMapToolStripMenuItem";
            buildMapToolStripMenuItem.Size = new System.Drawing.Size(411, 44);
            buildMapToolStripMenuItem.Text = "Build";
            buildMapToolStripMenuItem.Click += buildMapToolStripMenuItem_Click;
            // 
            // loadCABMapToolStripMenuItem
            // 
            loadCABMapToolStripMenuItem.Name = "loadCABMapToolStripMenuItem";
            loadCABMapToolStripMenuItem.Size = new System.Drawing.Size(411, 44);
            loadCABMapToolStripMenuItem.Text = "Load";
            loadCABMapToolStripMenuItem.Click += loadCABMapToolStripMenuItem_Click;
            // 
            // clearMapToolStripMenuItem
            // 
            clearMapToolStripMenuItem.Name = "clearMapToolStripMenuItem";
            clearMapToolStripMenuItem.Size = new System.Drawing.Size(411, 44);
            clearMapToolStripMenuItem.Text = "Delete";
            clearMapToolStripMenuItem.Click += clearMapToolStripMenuItem_Click;
            // 
            // toolStripSeparator15
            // 
            toolStripSeparator15.Name = "toolStripSeparator15";
            toolStripSeparator15.Size = new System.Drawing.Size(408, 6);
            // 
            // assetMapCABMapToolStripMenuItem
            // 
            assetMapCABMapToolStripMenuItem.Enabled = false;
            assetMapCABMapToolStripMenuItem.Name = "assetMapCABMapToolStripMenuItem";
            assetMapCABMapToolStripMenuItem.Size = new System.Drawing.Size(411, 44);
            assetMapCABMapToolStripMenuItem.Text = "Asset Map and CAB Map";
            // 
            // toolStripTextBox1
            // 
            toolStripTextBox1.Name = "toolStripTextBox1";
            toolStripTextBox1.Size = new System.Drawing.Size(100, 39);
            toolStripTextBox1.ToolTipText = "Enter name of map here";
            // 
            // buildBothToolStripMenuItem
            // 
            buildBothToolStripMenuItem.Name = "buildBothToolStripMenuItem";
            buildBothToolStripMenuItem.Size = new System.Drawing.Size(411, 44);
            buildBothToolStripMenuItem.Text = "Build Both";
            buildBothToolStripMenuItem.Click += buildBothToolStripMenuItem_Click;
            // 
            // toolStripSeparator14
            // 
            toolStripSeparator14.Name = "toolStripSeparator14";
            toolStripSeparator14.Size = new System.Drawing.Size(408, 6);
            // 
            // toolStripMenuItem20
            // 
            toolStripMenuItem20.Enabled = false;
            toolStripMenuItem20.Name = "toolStripMenuItem20";
            toolStripMenuItem20.Size = new System.Drawing.Size(411, 44);
            toolStripMenuItem20.Text = "Asset Index";
            // 
            // toolStripMenuItem19
            // 
            toolStripMenuItem19.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { specifyAIVersion });
            toolStripMenuItem19.Name = "toolStripMenuItem19";
            toolStripMenuItem19.Size = new System.Drawing.Size(411, 44);
            toolStripMenuItem19.Text = "Load from GitHub";
            // 
            // specifyAIVersion
            // 
            specifyAIVersion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            specifyAIVersion.Items.AddRange(new object[] { "None" });
            specifyAIVersion.Name = "specifyAIVersion";
            specifyAIVersion.Size = new System.Drawing.Size(121, 40);
            // 
            // loadAIToolStripMenuItem
            // 
            loadAIToolStripMenuItem.Name = "loadAIToolStripMenuItem";
            loadAIToolStripMenuItem.Size = new System.Drawing.Size(411, 44);
            loadAIToolStripMenuItem.Text = "Load from file";
            loadAIToolStripMenuItem.Click += loadAIToolStripMenuItem_Click;
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new System.Drawing.Size(99, 38);
            aboutToolStripMenuItem.Text = "About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // assetHelpersToolStripMenuItem
            // 
            assetHelpersToolStripMenuItem.Name = "assetHelpersToolStripMenuItem";
            assetHelpersToolStripMenuItem.Size = new System.Drawing.Size(32, 19);
            // 
            // MapToolStripMenuItem
            // 
            MapToolStripMenuItem.Name = "MapToolStripMenuItem";
            MapToolStripMenuItem.Size = new System.Drawing.Size(32, 19);
            // 
            // assetMapToolStripMenuItem
            // 
            assetMapToolStripMenuItem.Name = "assetMapToolStripMenuItem";
            assetMapToolStripMenuItem.Size = new System.Drawing.Size(32, 19);
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            toolStripSeparator5.Size = new System.Drawing.Size(178, 6);
            // 
            // splitContainer1
            // 
            splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer1.Location = new System.Drawing.Point(0, 42);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(tabControl1);
            splitContainer1.Panel1.Controls.Add(progressbarPanel);
            splitContainer1.Panel1MinSize = 200;
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(tabControl2);
            splitContainer1.Panel2.Controls.Add(statusStrip1);
            splitContainer1.Panel2MinSize = 400;
            splitContainer1.Size = new System.Drawing.Size(1582, 811);
            splitContainer1.SplitterDistance = 603;
            splitContainer1.TabIndex = 2;
            splitContainer1.TabStop = false;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            tabControl1.Location = new System.Drawing.Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.Padding = new System.Drawing.Point(17, 3);
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new System.Drawing.Size(601, 789);
            tabControl1.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            tabControl1.TabIndex = 0;
            tabControl1.Selected += tabPageSelected;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(sceneTreeView);
            tabPage1.Controls.Add(treeSearch);
            tabPage1.Location = new System.Drawing.Point(8, 46);
            tabPage1.Name = "tabPage1";
            tabPage1.Size = new System.Drawing.Size(585, 735);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Scene Hierarchy";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // sceneTreeView
            // 
            sceneTreeView.BackColor = System.Drawing.SystemColors.Window;
            sceneTreeView.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            sceneTreeView.CheckBoxes = true;
            sceneTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            sceneTreeView.HideSelection = false;
            sceneTreeView.Location = new System.Drawing.Point(0, 39);
            sceneTreeView.Name = "sceneTreeView";
            sceneTreeView.Size = new System.Drawing.Size(585, 696);
            sceneTreeView.TabIndex = 1;
            sceneTreeView.AfterCheck += sceneTreeView_AfterCheck;
            // 
            // treeSearch
            // 
            treeSearch.Dock = System.Windows.Forms.DockStyle.Top;
            treeSearch.ForeColor = System.Drawing.SystemColors.WindowText;
            treeSearch.Location = new System.Drawing.Point(0, 0);
            treeSearch.Name = "treeSearch";
            treeSearch.PlaceholderText = "Search (with Ctrl to check result, with Shift for all, alt for parent nodes)";
            treeSearch.Size = new System.Drawing.Size(585, 39);
            treeSearch.TabIndex = 0;
            treeSearch.TextChanged += treeSearch_TextChanged;
            treeSearch.KeyDown += treeSearch_KeyDown;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(assetListView);
            tabPage2.Controls.Add(listSearch);
            tabPage2.Location = new System.Drawing.Point(8, 46);
            tabPage2.Name = "tabPage2";
            tabPage2.Size = new System.Drawing.Size(585, 735);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Asset List";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // assetListView
            // 
            assetListView.BackColor = System.Drawing.SystemColors.Window;
            assetListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { columnHeaderName, columnHeaderContainer, columnHeaderType, columnHeaderPathID, columnHeaderSize, columnHeaderSHA256 });
            assetListView.Dock = System.Windows.Forms.DockStyle.Fill;
            assetListView.FullRowSelect = true;
            assetListView.GridLines = true;
            assetListView.Location = new System.Drawing.Point(0, 39);
            assetListView.Name = "assetListView";
            assetListView.Size = new System.Drawing.Size(585, 696);
            assetListView.TabIndex = 1;
            assetListView.UseCompatibleStateImageBehavior = false;
            assetListView.View = System.Windows.Forms.View.Details;
            assetListView.VirtualMode = true;
            assetListView.ColumnClick += assetListView_ColumnClick;
            assetListView.ItemSelectionChanged += selectAsset;
            assetListView.RetrieveVirtualItem += assetListView_RetrieveVirtualItem;
            assetListView.MouseClick += assetListView_MouseClick;
            // 
            // columnHeaderName
            // 
            columnHeaderName.Text = "Name";
            columnHeaderName.Width = 180;
            // 
            // columnHeaderContainer
            // 
            columnHeaderContainer.Text = "Container";
            columnHeaderContainer.Width = 80;
            // 
            // columnHeaderType
            // 
            columnHeaderType.Text = "Type";
            columnHeaderType.Width = 90;
            // 
            // columnHeaderPathID
            // 
            columnHeaderPathID.Text = "PathID";
            // 
            // columnHeaderSize
            // 
            columnHeaderSize.Text = "Size";
            columnHeaderSize.Width = 50;
            // 
            // columnHeaderSHA256
            // 
            columnHeaderSHA256.Text = "SHA256";
            columnHeaderSHA256.Width = 100;
            // 
            // listSearch
            // 
            listSearch.Dock = System.Windows.Forms.DockStyle.Top;
            listSearch.ForeColor = System.Drawing.SystemColors.WindowText;
            listSearch.Location = new System.Drawing.Point(0, 0);
            listSearch.Name = "listSearch";
            listSearch.PlaceholderText = "Search";
            listSearch.Size = new System.Drawing.Size(585, 39);
            listSearch.TabIndex = 0;
            listSearch.KeyPress += listSearch_KeyPress;
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(classesListView);
            tabPage3.Location = new System.Drawing.Point(8, 46);
            tabPage3.Name = "tabPage3";
            tabPage3.Size = new System.Drawing.Size(585, 735);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "Asset Classes";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // classesListView
            // 
            classesListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { columnHeader1, columnHeader2 });
            classesListView.Dock = System.Windows.Forms.DockStyle.Fill;
            classesListView.FullRowSelect = true;
            classesListView.Location = new System.Drawing.Point(0, 0);
            classesListView.MultiSelect = false;
            classesListView.Name = "classesListView";
            classesListView.Size = new System.Drawing.Size(585, 735);
            classesListView.TabIndex = 0;
            classesListView.UseCompatibleStateImageBehavior = false;
            classesListView.View = System.Windows.Forms.View.Details;
            classesListView.ItemSelectionChanged += classesListView_ItemSelectionChanged;
            // 
            // columnHeader1
            // 
            columnHeader1.DisplayIndex = 1;
            columnHeader1.Text = "Name";
            columnHeader1.Width = 300;
            // 
            // columnHeader2
            // 
            columnHeader2.DisplayIndex = 0;
            columnHeader2.Text = "ID";
            columnHeader2.Width = 70;
            // 
            // progressbarPanel
            // 
            progressbarPanel.Controls.Add(progressBar1);
            progressbarPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            progressbarPanel.Location = new System.Drawing.Point(0, 789);
            progressbarPanel.Name = "progressbarPanel";
            progressbarPanel.Padding = new System.Windows.Forms.Padding(1, 3, 1, 1);
            progressbarPanel.Size = new System.Drawing.Size(601, 20);
            progressbarPanel.TabIndex = 2;
            // 
            // progressBar1
            // 
            progressBar1.Dock = System.Windows.Forms.DockStyle.Bottom;
            progressBar1.ForeColor = System.Drawing.SystemColors.MenuHighlight;
            progressBar1.Location = new System.Drawing.Point(1, 2);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new System.Drawing.Size(599, 17);
            progressBar1.Step = 1;
            progressBar1.TabIndex = 1;
            // 
            // tabControl2
            // 
            tabControl2.Controls.Add(tabPage4);
            tabControl2.Controls.Add(tabPage5);
            tabControl2.Dock = System.Windows.Forms.DockStyle.Fill;
            tabControl2.Location = new System.Drawing.Point(0, 0);
            tabControl2.Name = "tabControl2";
            tabControl2.SelectedIndex = 0;
            tabControl2.Size = new System.Drawing.Size(973, 767);
            tabControl2.TabIndex = 4;
            tabControl2.SelectedIndexChanged += tabControl2_SelectedIndexChanged;
            // 
            // tabPage4
            // 
            tabPage4.Controls.Add(previewPanel);
            tabPage4.Location = new System.Drawing.Point(8, 46);
            tabPage4.Name = "tabPage4";
            tabPage4.Size = new System.Drawing.Size(957, 713);
            tabPage4.TabIndex = 0;
            tabPage4.Text = "Preview";
            tabPage4.UseVisualStyleBackColor = true;
            // 
            // previewPanel
            // 
            previewPanel.BackColor = System.Drawing.SystemColors.ControlDark;
            previewPanel.BackgroundImage = Properties.Resources.preview;
            previewPanel.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            previewPanel.Controls.Add(assetInfoLabel);
            previewPanel.Controls.Add(FMODpanel);
            previewPanel.Controls.Add(fontPreviewBox);
            previewPanel.Controls.Add(glControl);
            previewPanel.Controls.Add(textPreviewBox);
            previewPanel.Controls.Add(classTextBox);
            previewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            previewPanel.Location = new System.Drawing.Point(0, 0);
            previewPanel.Name = "previewPanel";
            previewPanel.Size = new System.Drawing.Size(957, 713);
            previewPanel.TabIndex = 1;
            previewPanel.Resize += preview_Resize;
            // 
            // assetInfoLabel
            // 
            assetInfoLabel.AutoSize = true;
            assetInfoLabel.BackColor = System.Drawing.Color.Transparent;
            assetInfoLabel.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            assetInfoLabel.Location = new System.Drawing.Point(4, 7);
            assetInfoLabel.Name = "assetInfoLabel";
            assetInfoLabel.Size = new System.Drawing.Size(0, 32);
            assetInfoLabel.TabIndex = 0;
            // 
            // FMODpanel
            // 
            FMODpanel.BackColor = System.Drawing.SystemColors.ControlDark;
            FMODpanel.Controls.Add(FMODcopyright);
            FMODpanel.Controls.Add(FMODinfoLabel);
            FMODpanel.Controls.Add(FMODtimerLabel);
            FMODpanel.Controls.Add(FMODstatusLabel);
            FMODpanel.Controls.Add(FMODprogressBar);
            FMODpanel.Controls.Add(FMODvolumeBar);
            FMODpanel.Controls.Add(FMODloopButton);
            FMODpanel.Controls.Add(FMODstopButton);
            FMODpanel.Controls.Add(FMODpauseButton);
            FMODpanel.Controls.Add(FMODplayButton);
            FMODpanel.Dock = System.Windows.Forms.DockStyle.Fill;
            FMODpanel.Location = new System.Drawing.Point(0, 0);
            FMODpanel.Name = "FMODpanel";
            FMODpanel.Size = new System.Drawing.Size(957, 713);
            FMODpanel.TabIndex = 2;
            FMODpanel.Visible = false;
            // 
            // FMODcopyright
            // 
            FMODcopyright.AutoSize = true;
            FMODcopyright.BackColor = System.Drawing.Color.Transparent;
            FMODcopyright.ForeColor = System.Drawing.Color.White;
            FMODcopyright.Location = new System.Drawing.Point(214, 337);
            FMODcopyright.Name = "FMODcopyright";
            FMODcopyright.Size = new System.Drawing.Size(643, 32);
            FMODcopyright.TabIndex = 9;
            FMODcopyright.Text = "Audio Engine supplied by FMOD by Firelight Technologies.";
            // 
            // FMODinfoLabel
            // 
            FMODinfoLabel.AutoSize = true;
            FMODinfoLabel.BackColor = System.Drawing.Color.Transparent;
            FMODinfoLabel.ForeColor = System.Drawing.Color.White;
            FMODinfoLabel.Location = new System.Drawing.Point(269, 235);
            FMODinfoLabel.Name = "FMODinfoLabel";
            FMODinfoLabel.Size = new System.Drawing.Size(0, 32);
            FMODinfoLabel.TabIndex = 8;
            // 
            // FMODtimerLabel
            // 
            FMODtimerLabel.AutoSize = true;
            FMODtimerLabel.BackColor = System.Drawing.Color.Transparent;
            FMODtimerLabel.ForeColor = System.Drawing.Color.White;
            FMODtimerLabel.Location = new System.Drawing.Point(460, 235);
            FMODtimerLabel.Name = "FMODtimerLabel";
            FMODtimerLabel.Size = new System.Drawing.Size(161, 32);
            FMODtimerLabel.TabIndex = 7;
            FMODtimerLabel.Text = "0:00.0 / 0:00.0";
            // 
            // FMODstatusLabel
            // 
            FMODstatusLabel.AutoSize = true;
            FMODstatusLabel.BackColor = System.Drawing.Color.Transparent;
            FMODstatusLabel.ForeColor = System.Drawing.Color.White;
            FMODstatusLabel.Location = new System.Drawing.Point(213, 235);
            FMODstatusLabel.Name = "FMODstatusLabel";
            FMODstatusLabel.Size = new System.Drawing.Size(103, 32);
            FMODstatusLabel.TabIndex = 6;
            FMODstatusLabel.Text = "Stopped";
            // 
            // FMODprogressBar
            // 
            FMODprogressBar.AutoSize = false;
            FMODprogressBar.Location = new System.Drawing.Point(213, 253);
            FMODprogressBar.Maximum = 1000;
            FMODprogressBar.Name = "FMODprogressBar";
            FMODprogressBar.Size = new System.Drawing.Size(350, 22);
            FMODprogressBar.TabIndex = 5;
            FMODprogressBar.TickStyle = System.Windows.Forms.TickStyle.None;
            FMODprogressBar.Scroll += FMODprogressBar_Scroll;
            FMODprogressBar.MouseDown += FMODprogressBar_MouseDown;
            FMODprogressBar.MouseUp += FMODprogressBar_MouseUp;
            // 
            // FMODvolumeBar
            // 
            FMODvolumeBar.LargeChange = 2;
            FMODvolumeBar.Location = new System.Drawing.Point(460, 280);
            FMODvolumeBar.Name = "FMODvolumeBar";
            FMODvolumeBar.Size = new System.Drawing.Size(104, 90);
            FMODvolumeBar.TabIndex = 4;
            FMODvolumeBar.TickStyle = System.Windows.Forms.TickStyle.Both;
            FMODvolumeBar.Value = 8;
            FMODvolumeBar.ValueChanged += FMODvolumeBar_ValueChanged;
            // 
            // FMODloopButton
            // 
            FMODloopButton.Appearance = System.Windows.Forms.Appearance.Button;
            FMODloopButton.BackColor = System.Drawing.SystemColors.ButtonFace;
            FMODloopButton.Location = new System.Drawing.Point(399, 280);
            FMODloopButton.Name = "FMODloopButton";
            FMODloopButton.Size = new System.Drawing.Size(55, 42);
            FMODloopButton.TabIndex = 3;
            FMODloopButton.Text = "Loop";
            FMODloopButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            FMODloopButton.UseVisualStyleBackColor = false;
            FMODloopButton.CheckedChanged += FMODloopButton_CheckedChanged;
            // 
            // FMODstopButton
            // 
            FMODstopButton.BackColor = System.Drawing.SystemColors.ButtonFace;
            FMODstopButton.Location = new System.Drawing.Point(338, 280);
            FMODstopButton.Name = "FMODstopButton";
            FMODstopButton.Size = new System.Drawing.Size(55, 42);
            FMODstopButton.TabIndex = 2;
            FMODstopButton.Text = "Stop";
            FMODstopButton.UseVisualStyleBackColor = false;
            FMODstopButton.Click += FMODstopButton_Click;
            // 
            // FMODpauseButton
            // 
            FMODpauseButton.BackColor = System.Drawing.SystemColors.ButtonFace;
            FMODpauseButton.Location = new System.Drawing.Point(277, 280);
            FMODpauseButton.Name = "FMODpauseButton";
            FMODpauseButton.Size = new System.Drawing.Size(55, 42);
            FMODpauseButton.TabIndex = 1;
            FMODpauseButton.Text = "Pause";
            FMODpauseButton.UseVisualStyleBackColor = false;
            FMODpauseButton.Click += FMODpauseButton_Click;
            // 
            // FMODplayButton
            // 
            FMODplayButton.BackColor = System.Drawing.SystemColors.ButtonFace;
            FMODplayButton.Location = new System.Drawing.Point(216, 280);
            FMODplayButton.Name = "FMODplayButton";
            FMODplayButton.Size = new System.Drawing.Size(55, 42);
            FMODplayButton.TabIndex = 0;
            FMODplayButton.Text = "Play";
            FMODplayButton.UseVisualStyleBackColor = false;
            FMODplayButton.Click += FMODplayButton_Click;
            // 
            // fontPreviewBox
            // 
            fontPreviewBox.BackColor = System.Drawing.SystemColors.Window;
            fontPreviewBox.Dock = System.Windows.Forms.DockStyle.Fill;
            fontPreviewBox.Location = new System.Drawing.Point(0, 0);
            fontPreviewBox.Name = "fontPreviewBox";
            fontPreviewBox.ReadOnly = true;
            fontPreviewBox.Size = new System.Drawing.Size(957, 713);
            fontPreviewBox.TabIndex = 0;
            fontPreviewBox.Text = resources.GetString("fontPreviewBox.Text");
            fontPreviewBox.Visible = false;
            fontPreviewBox.WordWrap = false;
            // 
            // glControl
            // 
            glControl.API = OpenTK.Windowing.Common.ContextAPI.OpenGL;
            glControl.APIVersion = new Version(3, 3, 0, 0);
            glControl.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            glControl.Dock = System.Windows.Forms.DockStyle.Fill;
            glControl.Flags = OpenTK.Windowing.Common.ContextFlags.Default;
            glControl.IsEventDriven = true;
            glControl.Location = new System.Drawing.Point(0, 0);
            glControl.Name = "glControl";
            glControl.Profile = OpenTK.Windowing.Common.ContextProfile.Core;
            glControl.SharedContext = null;
            glControl.Size = new System.Drawing.Size(957, 713);
            glControl.TabIndex = 4;
            glControl.Visible = false;
            glControl.Load += glControl_Load;
            glControl.Paint += glControl_Paint;
            glControl.MouseDown += glControl_MouseDown;
            glControl.MouseMove += glControl_MouseMove;
            glControl.MouseUp += glControl_MouseUp;
            glControl.MouseWheel += glControl_MouseWheel;
            // 
            // textPreviewBox
            // 
            textPreviewBox.BackColor = System.Drawing.SystemColors.Window;
            textPreviewBox.Dock = System.Windows.Forms.DockStyle.Fill;
            textPreviewBox.Font = new System.Drawing.Font("Consolas", 9.75F);
            textPreviewBox.Location = new System.Drawing.Point(0, 0);
            textPreviewBox.Multiline = true;
            textPreviewBox.Name = "textPreviewBox";
            textPreviewBox.ReadOnly = true;
            textPreviewBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            textPreviewBox.Size = new System.Drawing.Size(957, 713);
            textPreviewBox.TabIndex = 2;
            textPreviewBox.Visible = false;
            textPreviewBox.WordWrap = false;
            // 
            // classTextBox
            // 
            classTextBox.BackColor = System.Drawing.SystemColors.Window;
            classTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            classTextBox.Location = new System.Drawing.Point(0, 0);
            classTextBox.Multiline = true;
            classTextBox.Name = "classTextBox";
            classTextBox.ReadOnly = true;
            classTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            classTextBox.Size = new System.Drawing.Size(957, 713);
            classTextBox.TabIndex = 3;
            classTextBox.Visible = false;
            classTextBox.WordWrap = false;
            // 
            // tabPage5
            // 
            tabPage5.Controls.Add(dumpTextBox);
            tabPage5.Location = new System.Drawing.Point(8, 46);
            tabPage5.Name = "tabPage5";
            tabPage5.Size = new System.Drawing.Size(957, 713);
            tabPage5.TabIndex = 1;
            tabPage5.Text = "Dump";
            tabPage5.UseVisualStyleBackColor = true;
            // 
            // dumpTextBox
            // 
            dumpTextBox.BackColor = System.Drawing.SystemColors.Window;
            dumpTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            dumpTextBox.Location = new System.Drawing.Point(0, 0);
            dumpTextBox.Multiline = true;
            dumpTextBox.Name = "dumpTextBox";
            dumpTextBox.ReadOnly = true;
            dumpTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            dumpTextBox.Size = new System.Drawing.Size(957, 713);
            dumpTextBox.TabIndex = 0;
            dumpTextBox.WordWrap = false;
            // 
            // statusStrip1
            // 
            statusStrip1.BackColor = System.Drawing.SystemColors.MenuBar;
            statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripStatusLabel1 });
            statusStrip1.Location = new System.Drawing.Point(0, 767);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new System.Drawing.Size(973, 42);
            statusStrip1.TabIndex = 2;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.BackColor = System.Drawing.Color.Transparent;
            toolStripStatusLabel1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new System.Drawing.Size(958, 32);
            toolStripStatusLabel1.Spring = true;
            toolStripStatusLabel1.Text = "Ready to go";
            toolStripStatusLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // timer
            // 
            timer.Interval = 10;
            timer.Tick += timer_Tick;
            // 
            // openFileDialog1
            // 
            openFileDialog1.AddExtension = false;
            openFileDialog1.Filter = "All types|*.*";
            openFileDialog1.Multiselect = true;
            openFileDialog1.RestoreDirectory = true;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { copyToolStripMenuItem, exportSelectedAssetsToolStripMenuItem, exportAnimatorwithselectedAnimationClipMenuItem, goToSceneHierarchyToolStripMenuItem, showOriginalFileToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new System.Drawing.Size(547, 194);
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            copyToolStripMenuItem.Size = new System.Drawing.Size(546, 38);
            copyToolStripMenuItem.Text = "Copy text";
            copyToolStripMenuItem.Click += copyToolStripMenuItem_Click;
            // 
            // exportSelectedAssetsToolStripMenuItem
            // 
            exportSelectedAssetsToolStripMenuItem.Name = "exportSelectedAssetsToolStripMenuItem";
            exportSelectedAssetsToolStripMenuItem.Size = new System.Drawing.Size(546, 38);
            exportSelectedAssetsToolStripMenuItem.Text = "Export selected assets";
            exportSelectedAssetsToolStripMenuItem.Click += exportSelectedAssetsToolStripMenuItem_Click;
            // 
            // exportAnimatorwithselectedAnimationClipMenuItem
            // 
            exportAnimatorwithselectedAnimationClipMenuItem.Name = "exportAnimatorwithselectedAnimationClipMenuItem";
            exportAnimatorwithselectedAnimationClipMenuItem.Size = new System.Drawing.Size(546, 38);
            exportAnimatorwithselectedAnimationClipMenuItem.Text = "Export Animator + selected AnimationClips";
            exportAnimatorwithselectedAnimationClipMenuItem.Visible = false;
            exportAnimatorwithselectedAnimationClipMenuItem.Click += exportAnimatorwithAnimationClipMenuItem_Click;
            // 
            // goToSceneHierarchyToolStripMenuItem
            // 
            goToSceneHierarchyToolStripMenuItem.Name = "goToSceneHierarchyToolStripMenuItem";
            goToSceneHierarchyToolStripMenuItem.Size = new System.Drawing.Size(546, 38);
            goToSceneHierarchyToolStripMenuItem.Text = "Go to scene hierarchy";
            goToSceneHierarchyToolStripMenuItem.Visible = false;
            goToSceneHierarchyToolStripMenuItem.Click += goToSceneHierarchyToolStripMenuItem_Click;
            // 
            // showOriginalFileToolStripMenuItem
            // 
            showOriginalFileToolStripMenuItem.Name = "showOriginalFileToolStripMenuItem";
            showOriginalFileToolStripMenuItem.Size = new System.Drawing.Size(546, 38);
            showOriginalFileToolStripMenuItem.Text = "Show original file";
            showOriginalFileToolStripMenuItem.Visible = false;
            showOriginalFileToolStripMenuItem.Click += showOriginalFileToolStripMenuItem_Click;
            // 
            // MainForm
            // 
            AllowDrop = true;
            ClientSize = new System.Drawing.Size(1582, 853);
            Controls.Add(splitContainer1);
            Controls.Add(menuStrip1);
            Icon = Properties.Resources._as;
            KeyPreview = true;
            MainMenuStrip = menuStrip1;
            MinimumSize = new System.Drawing.Size(620, 372);
            Name = "MainForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "AnimeStudio.GUI";
            DragDrop += MainForm_DragDrop;
            DragEnter += MainForm_DragEnter;
            KeyDown += AnimeStudioForm_KeyDown;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            tabPage3.ResumeLayout(false);
            progressbarPanel.ResumeLayout(false);
            tabControl2.ResumeLayout(false);
            tabPage4.ResumeLayout(false);
            previewPanel.ResumeLayout(false);
            previewPanel.PerformLayout();
            FMODpanel.ResumeLayout(false);
            FMODpanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)FMODprogressBar).EndInit();
            ((System.ComponentModel.ISupportInitialize)FMODvolumeBar).EndInit();
            tabPage5.ResumeLayout(false);
            tabPage5.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            contextMenuStrip1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TextBox treeSearch;
        private System.Windows.Forms.TextBox listSearch;
        private System.Windows.Forms.ToolStripMenuItem loadFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadFolderToolStripMenuItem;
        private System.Windows.Forms.ListView assetListView;
        private System.Windows.Forms.ColumnHeader columnHeaderName;
        private System.Windows.Forms.ColumnHeader columnHeaderSize;
        private System.Windows.Forms.ColumnHeader columnHeaderType;
        private System.Windows.Forms.ToolStripMenuItem exportToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAllAssetsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedAssetsMenuItem;
        private System.Windows.Forms.Panel previewPanel;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.Panel progressbarPanel;
        private System.Windows.Forms.ToolStripMenuItem exportFilteredAssetsMenuItem;
        private System.Windows.Forms.Label assetInfoLabel;
        private System.Windows.Forms.TextBox textPreviewBox;
        private System.Windows.Forms.RichTextBox fontPreviewBox;
        private System.Windows.Forms.Panel FMODpanel;
        private System.Windows.Forms.TrackBar FMODvolumeBar;
        private System.Windows.Forms.CheckBox FMODloopButton;
        private System.Windows.Forms.Button FMODstopButton;
        private System.Windows.Forms.Button FMODpauseButton;
        private System.Windows.Forms.Button FMODplayButton;
        private System.Windows.Forms.TrackBar FMODprogressBar;
        private System.Windows.Forms.Label FMODstatusLabel;
        private System.Windows.Forms.Label FMODtimerLabel;
        private System.Windows.Forms.Label FMODinfoLabel;
        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem extractFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extractFolderToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.ToolStripMenuItem showExpOpt;
        private GOHierarchy sceneTreeView;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.ListView classesListView;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.TextBox classTextBox;
        private System.Windows.Forms.Label FMODcopyright;
        private OpenTK.GLControl.GLControl glControl;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem showOriginalFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAnimatorwithselectedAnimationClipMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedAssetsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem filterTypeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem allToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem goToSceneHierarchyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem4;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem5;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem6;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem3;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem7;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem8;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem9;
        private System.Windows.Forms.ColumnHeader columnHeaderContainer;
        private System.Windows.Forms.ColumnHeader columnHeaderPathID;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.TabControl tabControl2;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.TextBox dumpTextBox;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem10;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem11;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem12;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem13;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem14;
        private System.Windows.Forms.ToolStripTextBox specifyUnityVersion;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem18;
        private System.Windows.Forms.ToolStripComboBox specifyGame;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem16;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem17;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem24;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem25;
        private System.Windows.Forms.ToolStripMenuItem miscToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem assetHelpersToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem buildBothToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem buildAssetMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox MapNameComboBox;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripMenuItem resetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem abortStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem MapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem assetMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadAIToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator8;
        private System.Windows.Forms.ToolStripMenuItem buildMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem enableResolveDependencies;
        private System.Windows.Forms.ToolStripMenuItem skipContainer;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator12;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator13;
        private System.Windows.Forms.ToolStripMenuItem sceneHierarchy;
        private System.Windows.Forms.ToolStripMenuItem assetMapTypeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadCABMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem allowDuplicates;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripMenuItem appThemeToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox specifyTheme;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem20;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator14;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem19;
        private System.Windows.Forms.ToolStripComboBox specifyAIVersion;
        private System.Windows.Forms.ToolStripMenuItem cABMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem assetMapToolStripMenuItem1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator15;
        private System.Windows.Forms.ToolStripMenuItem assetMapCABMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripTextBox toolStripTextBox1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripMenuItem assetBrowserToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem generalToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem formatSpecificToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem assetsStructureToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem gameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem assetLoadingToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem generalToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem modelsIncludeAnimationClips;
        private System.Windows.Forms.ToolStripMenuItem modelsMerge;
        private System.Windows.Forms.ToolStripMenuItem modelsObjectsExportAll;
        private System.Windows.Forms.ToolStripMenuItem modelsObjectsExportSelected;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem28;
        private System.Windows.Forms.ToolStripMenuItem modelsNodesExportSelected;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem30;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem29;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem miscToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem21;
        private System.Windows.Forms.ToolStripMenuItem displayAll;
        private System.Windows.Forms.ToolStripMenuItem enablePreview;
        private System.Windows.Forms.ToolStripMenuItem enableModelPreview;
        private System.Windows.Forms.ToolStripMenuItem modelsOnly;
        private System.Windows.Forms.ToolStripMenuItem displayInfo;
        private System.Windows.Forms.ToolStripMenuItem debugToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem15;
        private System.Windows.Forms.ToolStripMenuItem exportClassStructuresMenuItem;
        private System.Windows.Forms.ToolStripMenuItem enableConsole;
        private System.Windows.Forms.ToolStripMenuItem clearConsoleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem enableFileLogging;
        private System.Windows.Forms.ToolStripMenuItem loggedEventsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ColumnHeader columnHeaderSHA256;
        private System.Windows.Forms.ToolStripMenuItem gameSelect;
        private System.Windows.Forms.ToolStripMenuItem editUnityCNKeysToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem useBundleContainerNameToolStripMenuItem;
    }
}

