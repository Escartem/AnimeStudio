namespace AnimeStudio.GUI
{
    partial class GameSelector
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GameSelector));
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            panel1 = new System.Windows.Forms.Panel();
            customKeyText = new System.Windows.Forms.TextBox();
            label4 = new System.Windows.Forms.Label();
            panel2 = new System.Windows.Forms.Panel();
            gameTypeCombo = new System.Windows.Forms.ComboBox();
            panel4 = new System.Windows.Forms.Panel();
            hoyoCombo = new System.Windows.Forms.ComboBox();
            label3 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            confirmBtn = new System.Windows.Forms.Button();
            cancelBtn = new System.Windows.Forms.Button();
            panel3 = new System.Windows.Forms.Panel();
            gameCombo = new System.Windows.Forms.ComboBox();
            tableLayoutPanel1.SuspendLayout();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            panel4.SuspendLayout();
            panel3.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 37.5F));
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 62.5F));
            tableLayoutPanel1.Controls.Add(panel1, 1, 3);
            tableLayoutPanel1.Controls.Add(label4, 0, 3);
            tableLayoutPanel1.Controls.Add(panel2, 1, 0);
            tableLayoutPanel1.Controls.Add(panel4, 1, 2);
            tableLayoutPanel1.Controls.Add(label3, 0, 2);
            tableLayoutPanel1.Controls.Add(label1, 0, 0);
            tableLayoutPanel1.Controls.Add(label2, 0, 1);
            tableLayoutPanel1.Controls.Add(confirmBtn, 1, 4);
            tableLayoutPanel1.Controls.Add(cancelBtn, 0, 4);
            tableLayoutPanel1.Controls.Add(panel3, 1, 1);
            tableLayoutPanel1.Location = new System.Drawing.Point(20, 19);
            tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 5;
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            tableLayoutPanel1.Size = new System.Drawing.Size(1069, 488);
            tableLayoutPanel1.TabIndex = 2;
            // 
            // panel1
            // 
            panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panel1.Controls.Add(customKeyText);
            panel1.Location = new System.Drawing.Point(405, 323);
            panel1.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(659, 96);
            panel1.TabIndex = 15;
            // 
            // customKeyText
            // 
            customKeyText.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            customKeyText.Enabled = false;
            customKeyText.Location = new System.Drawing.Point(81, 32);
            customKeyText.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            customKeyText.Name = "customKeyText";
            customKeyText.Size = new System.Drawing.Size(534, 39);
            customKeyText.TabIndex = 0;
            // 
            // label4
            // 
            label4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            label4.AutoSize = true;
            label4.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            label4.Location = new System.Drawing.Point(5, 318);
            label4.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(390, 106);
            label4.TabIndex = 14;
            label4.Text = "Custom Key (UnityCN only) :";
            label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel2
            // 
            panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panel2.Controls.Add(gameTypeCombo);
            panel2.Location = new System.Drawing.Point(405, 5);
            panel2.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            panel2.Name = "panel2";
            panel2.Size = new System.Drawing.Size(659, 96);
            panel2.TabIndex = 13;
            // 
            // gameTypeCombo
            // 
            gameTypeCombo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            gameTypeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            gameTypeCombo.FormattingEnabled = true;
            gameTypeCombo.Items.AddRange(new object[] { "Hoyo game", "Encrypted game (pick if unsure)", "Other" });
            gameTypeCombo.Location = new System.Drawing.Point(81, 24);
            gameTypeCombo.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            gameTypeCombo.Name = "gameTypeCombo";
            gameTypeCombo.Size = new System.Drawing.Size(534, 40);
            gameTypeCombo.TabIndex = 0;
            gameTypeCombo.SelectedIndexChanged += gameTypeCombo_SelectedIndexChanged;
            // 
            // panel4
            // 
            panel4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panel4.Controls.Add(hoyoCombo);
            panel4.Location = new System.Drawing.Point(405, 217);
            panel4.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            panel4.Name = "panel4";
            panel4.Size = new System.Drawing.Size(659, 96);
            panel4.TabIndex = 11;
            // 
            // hoyoCombo
            // 
            hoyoCombo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            hoyoCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            hoyoCombo.Enabled = false;
            hoyoCombo.FormattingEnabled = true;
            hoyoCombo.Location = new System.Drawing.Point(81, 24);
            hoyoCombo.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            hoyoCombo.Name = "hoyoCombo";
            hoyoCombo.Size = new System.Drawing.Size(534, 40);
            hoyoCombo.TabIndex = 0;
            hoyoCombo.SelectedIndexChanged += hoyoCombo_SelectedIndexChanged;
            // 
            // label3
            // 
            label3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            label3.AutoSize = true;
            label3.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            label3.Location = new System.Drawing.Point(5, 212);
            label3.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(390, 106);
            label3.TabIndex = 4;
            label3.Text = "Game version (Hoyo only) :";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            label1.AutoSize = true;
            label1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            label1.Location = new System.Drawing.Point(5, 0);
            label1.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(390, 106);
            label1.TabIndex = 0;
            label1.Text = "Game type :";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            label2.AutoSize = true;
            label2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            label2.Location = new System.Drawing.Point(5, 106);
            label2.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(390, 106);
            label2.TabIndex = 1;
            label2.Text = "Game :";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // confirmBtn
            // 
            confirmBtn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            confirmBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            confirmBtn.Location = new System.Drawing.Point(405, 429);
            confirmBtn.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            confirmBtn.Name = "confirmBtn";
            confirmBtn.Size = new System.Drawing.Size(659, 54);
            confirmBtn.TabIndex = 9;
            confirmBtn.Text = "Confirm";
            confirmBtn.UseVisualStyleBackColor = false;
            confirmBtn.Click += confirmBtn_Click;
            // 
            // cancelBtn
            // 
            cancelBtn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            cancelBtn.Location = new System.Drawing.Point(5, 429);
            cancelBtn.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            cancelBtn.Name = "cancelBtn";
            cancelBtn.Size = new System.Drawing.Size(390, 54);
            cancelBtn.TabIndex = 8;
            cancelBtn.Text = "Cancel";
            cancelBtn.UseVisualStyleBackColor = false;
            // 
            // panel3
            // 
            panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panel3.Controls.Add(gameCombo);
            panel3.Location = new System.Drawing.Point(405, 111);
            panel3.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            panel3.Name = "panel3";
            panel3.Size = new System.Drawing.Size(659, 96);
            panel3.TabIndex = 12;
            // 
            // gameCombo
            // 
            gameCombo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            gameCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            gameCombo.Enabled = false;
            gameCombo.FormattingEnabled = true;
            gameCombo.Location = new System.Drawing.Point(81, 24);
            gameCombo.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            gameCombo.Name = "gameCombo";
            gameCombo.Size = new System.Drawing.Size(534, 40);
            gameCombo.TabIndex = 0;
            gameCombo.SelectedIndexChanged += gameCombo_SelectedIndexChanged;
            // 
            // GameSelector
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = cancelBtn;
            ClientSize = new System.Drawing.Size(1095, 526);
            Controls.Add(tableLayoutPanel1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            MaximizeBox = false;
            MaximumSize = new System.Drawing.Size(1121, 597);
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(1121, 597);
            Name = "GameSelector";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Game Select";
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            panel2.ResumeLayout(false);
            panel4.ResumeLayout(false);
            panel3.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.ComboBox hoyoCombo;
        private System.Windows.Forms.Button confirmBtn;
        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.ComboBox gameCombo;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.ComboBox gameTypeCombo;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox customKeyText;
    }
}