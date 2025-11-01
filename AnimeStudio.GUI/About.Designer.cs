using System.Drawing;
using System.IO;

namespace AnimeStudio.GUI
{
    partial class AboutForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutForm));
            tabControl1 = new System.Windows.Forms.TabControl();
            tabPage1 = new System.Windows.Forms.TabPage();
            panel1 = new System.Windows.Forms.Panel();
            label2 = new System.Windows.Forms.Label();
            textBox2 = new System.Windows.Forms.TextBox();
            label11 = new System.Windows.Forms.Label();
            label10 = new System.Windows.Forms.Label();
            tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            gitAelurumLinkLabel = new System.Windows.Forms.LinkLabel();
            gitYarikLinkLabel = new System.Windows.Forms.LinkLabel();
            gitHashblenLinkLabel = new System.Windows.Forms.LinkLabel();
            gitRazmothLinkLabel = new System.Windows.Forms.LinkLabel();
            gitPerfareLinkLabel = new System.Windows.Forms.LinkLabel();
            label18 = new System.Windows.Forms.Label();
            label19 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            label6 = new System.Windows.Forms.Label();
            label9 = new System.Windows.Forms.Label();
            label12 = new System.Windows.Forms.Label();
            label13 = new System.Windows.Forms.Label();
            label14 = new System.Windows.Forms.Label();
            label15 = new System.Windows.Forms.Label();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            linkLabel2 = new System.Windows.Forms.LinkLabel();
            linkLabel1 = new System.Windows.Forms.LinkLabel();
            label5 = new System.Windows.Forms.Label();
            productNamelabel = new System.Windows.Forms.Label();
            label7 = new System.Windows.Forms.Label();
            modVersionLabel = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            checkUpdatesLinkLabel = new System.Windows.Forms.LinkLabel();
            label8 = new System.Windows.Forms.Label();
            gitEscartemLinkLabel = new System.Windows.Forms.LinkLabel();
            tabPage2 = new System.Windows.Forms.TabPage();
            licenseRichTextBox = new System.Windows.Forms.RichTextBox();
            pictureBox1 = new System.Windows.Forms.PictureBox();
            productTitleLabel = new System.Windows.Forms.Label();
            CloseButton = new System.Windows.Forms.Button();
            productVersionLabel = new System.Windows.Forms.Label();
            panel2 = new System.Windows.Forms.Panel();
            panel3 = new System.Windows.Forms.Panel();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            panel1.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            panel2.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Location = new Point(9, 119);
            tabControl1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(432, 423);
            tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.BackColor = SystemColors.Window;
            tabPage1.Controls.Add(panel1);
            tabPage1.Controls.Add(textBox2);
            tabPage1.Controls.Add(label11);
            tabPage1.Controls.Add(label10);
            tabPage1.Controls.Add(tableLayoutPanel2);
            tabPage1.Controls.Add(tableLayoutPanel1);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage1.Size = new Size(424, 395);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Info";
            // 
            // panel1
            // 
            panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panel1.BackColor = System.Drawing.Color.White;
            panel1.Controls.Add(label2);
            panel1.Location = new Point(10, 18);
            panel1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            panel1.Name = "panel1";
            panel1.Size = new Size(405, 37);
            panel1.TabIndex = 21;
            // 
            // label2
            // 
            label2.BackColor = SystemColors.Window;
            label2.Dock = System.Windows.Forms.DockStyle.Fill;
            label2.Location = new Point(0, 0);
            label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label2.MaximumSize = new Size(0, 35);
            label2.Name = "label2";
            label2.Size = new Size(405, 35);
            label2.TabIndex = 0;
            label2.Text = "Updated AssetStudio supporting GI, HSR, ZZZ (and more) with improvements and new features !";
            label2.UseCompatibleTextRendering = true;
            // 
            // textBox2
            // 
            textBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            textBox2.BackColor = SystemColors.Window;
            textBox2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            textBox2.Enabled = false;
            textBox2.ForeColor = SystemColors.GrayText;
            textBox2.Location = new Point(10, 295);
            textBox2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            textBox2.Multiline = true;
            textBox2.Name = "textBox2";
            textBox2.ReadOnly = true;
            textBox2.Size = new Size(405, 63);
            textBox2.TabIndex = 20;
            textBox2.TabStop = false;
            textBox2.Text = resources.GetString("textBox2.Text");
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(4, 164);
            label11.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label11.Name = "label11";
            label11.Size = new Size(96, 15);
            label11.TabIndex = 17;
            label11.Text = "Special thanks to";
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(7, 58);
            label10.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label10.Name = "label10";
            label10.Size = new Size(45, 15);
            label10.TabIndex = 16;
            label10.Text = "Version";
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.BackColor = SystemColors.Menu;
            tableLayoutPanel2.ColumnCount = 3;
            tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 41.37931F));
            tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 58.62069F));
            tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 135F));
            tableLayoutPanel2.Controls.Add(gitAelurumLinkLabel, 2, 4);
            tableLayoutPanel2.Controls.Add(gitYarikLinkLabel, 2, 0);
            tableLayoutPanel2.Controls.Add(gitHashblenLinkLabel, 2, 1);
            tableLayoutPanel2.Controls.Add(gitRazmothLinkLabel, 2, 2);
            tableLayoutPanel2.Controls.Add(gitPerfareLinkLabel, 2, 3);
            tableLayoutPanel2.Controls.Add(label18, 1, 0);
            tableLayoutPanel2.Controls.Add(label19, 0, 0);
            tableLayoutPanel2.Controls.Add(label1, 1, 1);
            tableLayoutPanel2.Controls.Add(label3, 0, 1);
            tableLayoutPanel2.Controls.Add(label6, 1, 2);
            tableLayoutPanel2.Controls.Add(label9, 0, 2);
            tableLayoutPanel2.Controls.Add(label12, 1, 3);
            tableLayoutPanel2.Controls.Add(label13, 0, 3);
            tableLayoutPanel2.Controls.Add(label14, 1, 4);
            tableLayoutPanel2.Controls.Add(label15, 0, 4);
            tableLayoutPanel2.Location = new Point(7, 181);
            tableLayoutPanel2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.Padding = new System.Windows.Forms.Padding(2);
            tableLayoutPanel2.RowCount = 4;
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            tableLayoutPanel2.Size = new Size(408, 107);
            tableLayoutPanel2.TabIndex = 15;
            // 
            // gitAelurumLinkLabel
            // 
            gitAelurumLinkLabel.AutoSize = true;
            gitAelurumLinkLabel.BackColor = System.Drawing.Color.Transparent;
            gitAelurumLinkLabel.LinkColor = SystemColors.MenuHighlight;
            gitAelurumLinkLabel.Location = new Point(274, 84);
            gitAelurumLinkLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            gitAelurumLinkLabel.Name = "gitAelurumLinkLabel";
            gitAelurumLinkLabel.Size = new Size(73, 15);
            gitAelurumLinkLabel.TabIndex = 26;
            gitAelurumLinkLabel.TabStop = true;
            gitAelurumLinkLabel.Text = "Project page";
            gitAelurumLinkLabel.LinkClicked += gitAelurumLinkLabel_LinkClicked;
            // 
            // gitYarikLinkLabel
            // 
            gitYarikLinkLabel.AutoSize = true;
            gitYarikLinkLabel.BackColor = System.Drawing.Color.Transparent;
            gitYarikLinkLabel.LinkColor = SystemColors.MenuHighlight;
            gitYarikLinkLabel.Location = new Point(274, 2);
            gitYarikLinkLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            gitYarikLinkLabel.Name = "gitYarikLinkLabel";
            gitYarikLinkLabel.Size = new Size(73, 15);
            gitYarikLinkLabel.TabIndex = 14;
            gitYarikLinkLabel.TabStop = true;
            gitYarikLinkLabel.Text = "Project page";
            gitYarikLinkLabel.LinkClicked += gitYarikLinkLabel_LinkClicked;
            // 
            // gitHashblenLinkLabel
            // 
            gitHashblenLinkLabel.AutoSize = true;
            gitHashblenLinkLabel.BackColor = System.Drawing.Color.Transparent;
            gitHashblenLinkLabel.LinkColor = SystemColors.MenuHighlight;
            gitHashblenLinkLabel.Location = new Point(274, 23);
            gitHashblenLinkLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            gitHashblenLinkLabel.Name = "gitHashblenLinkLabel";
            gitHashblenLinkLabel.Size = new Size(73, 15);
            gitHashblenLinkLabel.TabIndex = 17;
            gitHashblenLinkLabel.TabStop = true;
            gitHashblenLinkLabel.Text = "Project page";
            gitHashblenLinkLabel.LinkClicked += gitHashblenLinkLabel_LinkClicked;
            // 
            // gitRazmothLinkLabel
            // 
            gitRazmothLinkLabel.AutoSize = true;
            gitRazmothLinkLabel.BackColor = System.Drawing.Color.Transparent;
            gitRazmothLinkLabel.LinkColor = SystemColors.MenuHighlight;
            gitRazmothLinkLabel.Location = new Point(274, 44);
            gitRazmothLinkLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            gitRazmothLinkLabel.Name = "gitRazmothLinkLabel";
            gitRazmothLinkLabel.Size = new Size(73, 15);
            gitRazmothLinkLabel.TabIndex = 20;
            gitRazmothLinkLabel.TabStop = true;
            gitRazmothLinkLabel.Text = "Project page";
            gitRazmothLinkLabel.LinkClicked += gitRazmothLinkLabel_LinkClicked;
            // 
            // gitPerfareLinkLabel
            // 
            gitPerfareLinkLabel.AutoSize = true;
            gitPerfareLinkLabel.BackColor = System.Drawing.Color.Transparent;
            gitPerfareLinkLabel.LinkColor = SystemColors.MenuHighlight;
            gitPerfareLinkLabel.Location = new Point(274, 64);
            gitPerfareLinkLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            gitPerfareLinkLabel.Name = "gitPerfareLinkLabel";
            gitPerfareLinkLabel.Size = new Size(73, 15);
            gitPerfareLinkLabel.TabIndex = 23;
            gitPerfareLinkLabel.TabStop = true;
            gitPerfareLinkLabel.Text = "Project page";
            gitPerfareLinkLabel.LinkClicked += gitPerfareLinkLabel_LinkClicked;
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.BackColor = System.Drawing.Color.Transparent;
            label18.Location = new Point(117, 2);
            label18.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label18.Name = "label18";
            label18.Size = new Size(69, 15);
            label18.TabIndex = 12;
            label18.Text = "Yarik Studio";
            // 
            // label19
            // 
            label19.AutoSize = true;
            label19.BackColor = System.Drawing.Color.Transparent;
            label19.Location = new Point(6, 2);
            label19.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label19.Name = "label19";
            label19.Size = new Size(63, 15);
            label19.TabIndex = 13;
            label19.Text = "yarik0chka";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.BackColor = System.Drawing.Color.Transparent;
            label1.Location = new Point(117, 23);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(65, 15);
            label1.TabIndex = 15;
            label1.Text = "ZZZ Studio";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.BackColor = System.Drawing.Color.Transparent;
            label3.Location = new Point(6, 23);
            label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(57, 15);
            label3.TabIndex = 16;
            label3.Text = "Hashblen";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.BackColor = System.Drawing.Color.Transparent;
            label6.Location = new Point(117, 44);
            label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label6.Name = "label6";
            label6.Size = new Size(72, 15);
            label6.TabIndex = 18;
            label6.Text = "Asset Studio";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.BackColor = System.Drawing.Color.Transparent;
            label9.Location = new Point(6, 44);
            label9.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label9.Name = "label9";
            label9.Size = new Size(54, 15);
            label9.TabIndex = 19;
            label9.Text = "Razmoth";
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.BackColor = System.Drawing.Color.Transparent;
            label12.Location = new Point(117, 64);
            label12.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label12.Name = "label12";
            label12.Size = new Size(86, 15);
            label12.TabIndex = 21;
            label12.Text = "Original Studio";
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.BackColor = System.Drawing.Color.Transparent;
            label13.Location = new Point(6, 64);
            label13.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label13.Name = "label13";
            label13.Size = new Size(47, 15);
            label13.TabIndex = 22;
            label13.Text = "Perfare ";
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.BackColor = System.Drawing.Color.Transparent;
            label14.Location = new Point(117, 84);
            label14.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label14.Name = "label14";
            label14.Size = new Size(100, 15);
            label14.TabIndex = 24;
            label14.Text = "Asset Studio Mod";
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.BackColor = System.Drawing.Color.Transparent;
            label15.Location = new Point(6, 84);
            label15.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label15.Name = "label15";
            label15.Size = new Size(51, 15);
            label15.TabIndex = 25;
            label15.Text = "aelurum";
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.BackColor = SystemColors.Menu;
            tableLayoutPanel1.ColumnCount = 3;
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 41.37931F));
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 58.62069F));
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 135F));
            tableLayoutPanel1.Controls.Add(linkLabel2, 0, 3);
            tableLayoutPanel1.Controls.Add(linkLabel1, 2, 0);
            tableLayoutPanel1.Controls.Add(label5, 0, 0);
            tableLayoutPanel1.Controls.Add(productNamelabel, 1, 0);
            tableLayoutPanel1.Controls.Add(label7, 0, 1);
            tableLayoutPanel1.Controls.Add(modVersionLabel, 1, 1);
            tableLayoutPanel1.Controls.Add(label4, 0, 2);
            tableLayoutPanel1.Controls.Add(checkUpdatesLinkLabel, 2, 1);
            tableLayoutPanel1.Controls.Add(label8, 1, 2);
            tableLayoutPanel1.Controls.Add(gitEscartemLinkLabel, 2, 2);
            tableLayoutPanel1.Location = new Point(7, 76);
            tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(2);
            tableLayoutPanel1.RowCount = 4;
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 15F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 23F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 23F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 23F));
            tableLayoutPanel1.Size = new Size(408, 85);
            tableLayoutPanel1.TabIndex = 5;
            // 
            // linkLabel2
            // 
            linkLabel2.AutoSize = true;
            linkLabel2.BackColor = System.Drawing.Color.Transparent;
            tableLayoutPanel1.SetColumnSpan(linkLabel2, 3);
            linkLabel2.Dock = System.Windows.Forms.DockStyle.Fill;
            linkLabel2.LinkBehavior = System.Windows.Forms.LinkBehavior.AlwaysUnderline;
            linkLabel2.LinkColor = SystemColors.MenuHighlight;
            linkLabel2.Location = new Point(6, 66);
            linkLabel2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            linkLabel2.Name = "linkLabel2";
            linkLabel2.Size = new Size(396, 17);
            linkLabel2.TabIndex = 14;
            linkLabel2.TabStop = true;
            linkLabel2.Text = "Report a bug";
            linkLabel2.TextAlign = ContentAlignment.MiddleCenter;
            linkLabel2.VisitedLinkColor = System.Drawing.Color.Fuchsia;
            linkLabel2.LinkClicked += linkLabel2_LinkClicked;
            // 
            // linkLabel1
            // 
            linkLabel1.AutoSize = true;
            linkLabel1.BackColor = System.Drawing.Color.Transparent;
            linkLabel1.Dock = System.Windows.Forms.DockStyle.Fill;
            linkLabel1.LinkColor = SystemColors.MenuHighlight;
            linkLabel1.Location = new Point(274, 2);
            linkLabel1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            linkLabel1.Name = "linkLabel1";
            linkLabel1.Size = new Size(128, 22);
            linkLabel1.TabIndex = 13;
            linkLabel1.TabStop = true;
            linkLabel1.Text = "Project page";
            linkLabel1.TextAlign = ContentAlignment.MiddleCenter;
            linkLabel1.LinkClicked += linkLabel1_LinkClicked;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.BackColor = System.Drawing.Color.Transparent;
            label5.Dock = System.Windows.Forms.DockStyle.Fill;
            label5.Location = new Point(6, 2);
            label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label5.Name = "label5";
            label5.Size = new Size(103, 22);
            label5.TabIndex = 0;
            label5.Text = "Product name :";
            label5.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // productNamelabel
            // 
            productNamelabel.AutoSize = true;
            productNamelabel.BackColor = System.Drawing.Color.Transparent;
            productNamelabel.Dock = System.Windows.Forms.DockStyle.Fill;
            productNamelabel.Location = new Point(117, 2);
            productNamelabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            productNamelabel.Name = "productNamelabel";
            productNamelabel.Size = new Size(149, 22);
            productNamelabel.TabIndex = 1;
            productNamelabel.Text = "AnimeStudio";
            productNamelabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.BackColor = System.Drawing.Color.Transparent;
            label7.Dock = System.Windows.Forms.DockStyle.Fill;
            label7.Location = new Point(6, 24);
            label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label7.Name = "label7";
            label7.Size = new Size(103, 21);
            label7.TabIndex = 2;
            label7.Text = "Version :";
            label7.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // modVersionLabel
            // 
            modVersionLabel.AutoSize = true;
            modVersionLabel.BackColor = System.Drawing.Color.Transparent;
            modVersionLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            modVersionLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            modVersionLabel.Location = new Point(117, 24);
            modVersionLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            modVersionLabel.Name = "modVersionLabel";
            modVersionLabel.Size = new Size(149, 21);
            modVersionLabel.TabIndex = 3;
            modVersionLabel.Text = "1.0.0";
            modVersionLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.BackColor = System.Drawing.Color.Transparent;
            label4.Dock = System.Windows.Forms.DockStyle.Fill;
            label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label4.Location = new Point(6, 45);
            label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new Size(103, 21);
            label4.TabIndex = 4;
            label4.Text = "Made with love by :";
            label4.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // checkUpdatesLinkLabel
            // 
            checkUpdatesLinkLabel.AutoSize = true;
            checkUpdatesLinkLabel.BackColor = System.Drawing.Color.Transparent;
            checkUpdatesLinkLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            checkUpdatesLinkLabel.LinkColor = SystemColors.MenuHighlight;
            checkUpdatesLinkLabel.Location = new Point(274, 24);
            checkUpdatesLinkLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            checkUpdatesLinkLabel.Name = "checkUpdatesLinkLabel";
            checkUpdatesLinkLabel.Size = new Size(128, 21);
            checkUpdatesLinkLabel.TabIndex = 6;
            checkUpdatesLinkLabel.TabStop = true;
            checkUpdatesLinkLabel.Text = "Check for Updates";
            checkUpdatesLinkLabel.TextAlign = ContentAlignment.MiddleCenter;
            checkUpdatesLinkLabel.LinkClicked += checkUpdatesLinkLabel_LinkClicked;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.BackColor = System.Drawing.Color.Transparent;
            label8.Dock = System.Windows.Forms.DockStyle.Fill;
            label8.Location = new Point(117, 45);
            label8.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label8.Name = "label8";
            label8.Size = new Size(149, 21);
            label8.TabIndex = 11;
            label8.Text = "Escartem <3";
            label8.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // gitEscartemLinkLabel
            // 
            gitEscartemLinkLabel.AutoSize = true;
            gitEscartemLinkLabel.BackColor = System.Drawing.Color.Transparent;
            gitEscartemLinkLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            gitEscartemLinkLabel.LinkBehavior = System.Windows.Forms.LinkBehavior.AlwaysUnderline;
            gitEscartemLinkLabel.LinkColor = SystemColors.MenuHighlight;
            gitEscartemLinkLabel.Location = new Point(274, 45);
            gitEscartemLinkLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            gitEscartemLinkLabel.Name = "gitEscartemLinkLabel";
            gitEscartemLinkLabel.Size = new Size(128, 21);
            gitEscartemLinkLabel.TabIndex = 12;
            gitEscartemLinkLabel.TabStop = true;
            gitEscartemLinkLabel.Text = "Github Profile";
            gitEscartemLinkLabel.TextAlign = ContentAlignment.MiddleCenter;
            gitEscartemLinkLabel.VisitedLinkColor = System.Drawing.Color.Fuchsia;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(licenseRichTextBox);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage2.Size = new Size(424, 395);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "License";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // licenseRichTextBox
            // 
            licenseRichTextBox.BackColor = SystemColors.Window;
            licenseRichTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            licenseRichTextBox.DetectUrls = false;
            licenseRichTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            licenseRichTextBox.Location = new Point(4, 3);
            licenseRichTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            licenseRichTextBox.Name = "licenseRichTextBox";
            licenseRichTextBox.ReadOnly = true;
            licenseRichTextBox.Size = new Size(416, 389);
            licenseRichTextBox.TabIndex = 0;
            licenseRichTextBox.Text = "MIT License";
            licenseRichTextBox.ZoomFactor = 1.1F;
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = System.Drawing.Color.Transparent;
            pictureBox1.Dock = System.Windows.Forms.DockStyle.Top;
            pictureBox1.Image = Properties.Resources.logo;
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(448, 58);
            pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // productTitleLabel
            // 
            productTitleLabel.BackColor = System.Drawing.Color.Transparent;
            productTitleLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            productTitleLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, FontStyle.Regular, GraphicsUnit.Point, 0);
            productTitleLabel.Location = new Point(0, 60);
            productTitleLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            productTitleLabel.Name = "productTitleLabel";
            productTitleLabel.Size = new Size(448, 35);
            productTitleLabel.TabIndex = 1;
            productTitleLabel.Text = "AnimeStudio";
            productTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // CloseButton
            // 
            CloseButton.BackColor = SystemColors.ButtonFace;
            CloseButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            CloseButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            CloseButton.Location = new Point(0, 486);
            CloseButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            CloseButton.Name = "CloseButton";
            CloseButton.Size = new Size(448, 45);
            CloseButton.TabIndex = 1;
            CloseButton.Text = "Close";
            CloseButton.UseVisualStyleBackColor = false;
            CloseButton.Click += CloseButton_Click;
            // 
            // productVersionLabel
            // 
            productVersionLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            productVersionLabel.ForeColor = SystemColors.GrayText;
            productVersionLabel.Location = new Point(0, 95);
            productVersionLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            productVersionLabel.Name = "productVersionLabel";
            productVersionLabel.Padding = new System.Windows.Forms.Padding(0, 0, 6, 0);
            productVersionLabel.Size = new Size(448, 15);
            productVersionLabel.TabIndex = 2;
            productVersionLabel.Text = "v1.0.0 [x64]";
            productVersionLabel.TextAlign = ContentAlignment.BottomCenter;
            // 
            // panel2
            // 
            panel2.Controls.Add(productTitleLabel);
            panel2.Controls.Add(pictureBox1);
            panel2.Controls.Add(productVersionLabel);
            panel2.Dock = System.Windows.Forms.DockStyle.Top;
            panel2.Location = new Point(0, 6);
            panel2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            panel2.Name = "panel2";
            panel2.Size = new Size(448, 110);
            panel2.TabIndex = 3;
            // 
            // panel3
            // 
            panel3.Dock = System.Windows.Forms.DockStyle.Top;
            panel3.Location = new Point(0, 0);
            panel3.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            panel3.Name = "panel3";
            panel3.Size = new Size(448, 6);
            panel3.TabIndex = 3;
            // 
            // AboutForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = CloseButton;
            ClientSize = new Size(448, 531);
            Controls.Add(panel2);
            Controls.Add(panel3);
            Controls.Add(CloseButton);
            Controls.Add(tabControl1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AboutForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "About";
            TopMost = true;
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            panel1.ResumeLayout(false);
            tableLayoutPanel2.ResumeLayout(false);
            tableLayoutPanel2.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            panel2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Label productTitleLabel;
        private System.Windows.Forms.Button CloseButton;
        private System.Windows.Forms.Label productVersionLabel;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.LinkLabel gitYarikLinkLabel;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label productNamelabel;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label modVersionLabel;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.LinkLabel checkUpdatesLinkLabel;
        private System.Windows.Forms.RichTextBox licenseRichTextBox;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.LinkLabel gitHashblenLinkLabel;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.LinkLabel gitRazmothLinkLabel;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.LinkLabel gitPerfareLinkLabel;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.LinkLabel gitEscartemLinkLabel;
        private System.Windows.Forms.LinkLabel gitAelurumLinkLabel;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.LinkLabel linkLabel2;
    }
}