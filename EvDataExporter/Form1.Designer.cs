namespace EvDataExporter
{
    partial class EvDataExporter
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EvDataExporter));
            notifyIcon1 = new NotifyIcon(components);
            lblSource = new Label();
            lblSourceServer = new Label();
            txtSourceServer = new TextBox();
            lblSourceDb = new Label();
            txtSourceDb = new TextBox();
            picSourceStatus = new PictureBox();
            this.lblOutputServer = new Label();
            txtOutputServer = new TextBox();
            this.lblOutputDb = new Label();
            txtOutputDb = new TextBox();
            picOutputStatus = new PictureBox();
            lblSavePath = new Label();
            txtSavePath = new TextBox();
            panelDivider = new Panel();
            cardTotal = new Panel();
            lblTotalVal = new Label();
            lblTotalTitle = new Label();
            cardExported = new Panel();
            lblExportedVal = new Label();
            lblExportedTitle = new Label();
            cardPct = new Panel();
            lblPctVal = new Label();
            lblPctTitle = new Label();
            btnToggle = new Button();
            btnSettings = new Button();
            panelStatusBar = new Panel();
            picDot = new PictureBox();
            lblStatus = new Label();
            lblLastExport = new Label();
            ((System.ComponentModel.ISupportInitialize)picSourceStatus).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picOutputStatus).BeginInit();
            cardTotal.SuspendLayout();
            cardExported.SuspendLayout();
            cardPct.SuspendLayout();
            panelStatusBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picDot).BeginInit();
            SuspendLayout();
            // 
            // notifyIcon1
            // 
            notifyIcon1.Icon = (Icon)resources.GetObject("notifyIcon1.Icon");
            notifyIcon1.Text = "EvDataExporter";
            notifyIcon1.Visible = true;
            // 
            // lblSource
            // 
            lblSource.BackColor = Color.Transparent;
            lblSource.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            lblSource.ForeColor = Color.FromArgb(120, 119, 112);
            lblSource.Location = new Point(12, 17);
            lblSource.Name = "lblSource";
            lblSource.Size = new Size(42, 15);
            lblSource.TabIndex = 0;
            lblSource.Text = "Source";
            // 
            // lblSourceServer
            // 
            lblSourceServer.BackColor = Color.Transparent;
            lblSourceServer.Font = new Font("Segoe UI", 8F);
            lblSourceServer.ForeColor = Color.FromArgb(120, 119, 112);
            lblSourceServer.Location = new Point(58, 17);
            lblSourceServer.Name = "lblSourceServer";
            lblSourceServer.Size = new Size(38, 15);
            lblSourceServer.TabIndex = 1;
            lblSourceServer.Text = "Server";
            // 
            // txtSourceServer
            // 
            txtSourceServer.BackColor = Color.White;
            txtSourceServer.BorderStyle = BorderStyle.FixedSingle;
            txtSourceServer.Font = new Font("Consolas", 7.5F);
            txtSourceServer.ForeColor = Color.FromArgb(30, 30, 28);
            txtSourceServer.Location = new Point(98, 13);
            txtSourceServer.Name = "txtSourceServer";
            txtSourceServer.Size = new Size(138, 19);
            txtSourceServer.TabIndex = 2;
            txtSourceServer.Text = "192.168.1.10\\SQLEXPRESS";
            // 
            // lblSourceDb
            // 
            lblSourceDb.BackColor = Color.Transparent;
            lblSourceDb.Font = new Font("Segoe UI", 8F);
            lblSourceDb.ForeColor = Color.FromArgb(120, 119, 112);
            lblSourceDb.Location = new Point(242, 17);
            lblSourceDb.Name = "lblSourceDb";
            lblSourceDb.Size = new Size(52, 15);
            lblSourceDb.TabIndex = 3;
            lblSourceDb.Text = "Database";
            // 
            // txtSourceDb
            // 
            txtSourceDb.BackColor = Color.White;
            txtSourceDb.BorderStyle = BorderStyle.FixedSingle;
            txtSourceDb.Font = new Font("Consolas", 7.5F);
            txtSourceDb.ForeColor = Color.FromArgb(30, 30, 28);
            txtSourceDb.Location = new Point(298, 13);
            txtSourceDb.Name = "txtSourceDb";
            txtSourceDb.Size = new Size(148, 19);
            txtSourceDb.TabIndex = 4;
            txtSourceDb.Text = "EV_Production_DB";
            // 
            // picSourceStatus
            // 
            picSourceStatus.BackColor = Color.Transparent;
            picSourceStatus.Location = new Point(452, 18);
            picSourceStatus.Name = "picSourceStatus";
            picSourceStatus.Size = new Size(10, 10);
            picSourceStatus.TabIndex = 5;
            picSourceStatus.TabStop = false;
            // 
            // lblOutputServer
            // 
            this.lblOutputServer.BackColor = Color.Transparent;
            this.lblOutputServer.Font = new Font("Segoe UI", 8F);
            this.lblOutputServer.ForeColor = Color.FromArgb(120, 119, 112);
            this.lblOutputServer.Location = new Point(58, 51);
            this.lblOutputServer.Name = "lblOutputServer";
            this.lblOutputServer.Size = new Size(38, 15);
            this.lblOutputServer.TabIndex = 7;
            this.lblOutputServer.Text = "Server";
            // 
            // txtOutputServer
            // 
            txtOutputServer.BackColor = Color.White;
            txtOutputServer.BorderStyle = BorderStyle.FixedSingle;
            txtOutputServer.Font = new Font("Consolas", 7.5F);
            txtOutputServer.ForeColor = Color.FromArgb(30, 30, 28);
            txtOutputServer.Location = new Point(98, 47);
            txtOutputServer.Name = "txtOutputServer";
            txtOutputServer.Size = new Size(138, 19);
            txtOutputServer.TabIndex = 8;
            txtOutputServer.Text = "192.168.1.20\\SQLEXPRESS";
            // 
            // lblOutputDb
            // 
            this.lblOutputDb.BackColor = Color.Transparent;
            this.lblOutputDb.Font = new Font("Segoe UI", 8F);
            this.lblOutputDb.ForeColor = Color.FromArgb(120, 119, 112);
            this.lblOutputDb.Location = new Point(242, 51);
            this.lblOutputDb.Name = "lblOutputDb";
            this.lblOutputDb.Size = new Size(52, 15);
            this.lblOutputDb.TabIndex = 9;
            this.lblOutputDb.Text = "Database";
            // 
            // txtOutputDb
            // 
            txtOutputDb.BackColor = Color.White;
            txtOutputDb.BorderStyle = BorderStyle.FixedSingle;
            txtOutputDb.Font = new Font("Consolas", 7.5F);
            txtOutputDb.ForeColor = Color.FromArgb(30, 30, 28);
            txtOutputDb.Location = new Point(298, 47);
            txtOutputDb.Name = "txtOutputDb";
            txtOutputDb.Size = new Size(148, 19);
            txtOutputDb.TabIndex = 10;
            txtOutputDb.Text = "EV_Path_Store_DB";
            // 
            // picOutputStatus
            // 
            picOutputStatus.BackColor = Color.Transparent;
            picOutputStatus.Location = new Point(452, 52);
            picOutputStatus.Name = "picOutputStatus";
            picOutputStatus.Size = new Size(10, 10);
            picOutputStatus.TabIndex = 11;
            picOutputStatus.TabStop = false;
            // 
            // lblSavePath
            // 
            lblSavePath.BackColor = Color.Transparent;
            lblSavePath.Font = new Font("Segoe UI", 8F);
            lblSavePath.ForeColor = Color.FromArgb(120, 119, 112);
            lblSavePath.Location = new Point(58, 85);
            lblSavePath.Name = "lblSavePath";
            lblSavePath.Size = new Size(52, 15);
            lblSavePath.TabIndex = 12;
            lblSavePath.Text = "Save path";
            // 
            // txtSavePath
            // 
            txtSavePath.BackColor = Color.White;
            txtSavePath.BorderStyle = BorderStyle.FixedSingle;
            txtSavePath.Font = new Font("Consolas", 7.5F);
            txtSavePath.ForeColor = Color.FromArgb(30, 30, 28);
            txtSavePath.Location = new Point(114, 81);
            txtSavePath.Name = "txtSavePath";
            txtSavePath.Size = new Size(332, 19);
            txtSavePath.TabIndex = 13;
            txtSavePath.Text = "C:\\EV_Data\\EV_Export_20260402.csv";
            // 
            // panelDivider
            // 
            panelDivider.BackColor = Color.FromArgb(220, 220, 215);
            panelDivider.Location = new Point(12, 113);
            panelDivider.Name = "panelDivider";
            panelDivider.Size = new Size(489, 1);
            panelDivider.TabIndex = 14;
            // 
            // cardTotal
            // 
            cardTotal.BackColor = Color.White;
            cardTotal.BorderStyle = BorderStyle.FixedSingle;
            cardTotal.Controls.Add(lblTotalVal);
            cardTotal.Controls.Add(lblTotalTitle);
            cardTotal.Location = new Point(12, 122);
            cardTotal.Name = "cardTotal";
            cardTotal.Size = new Size(152, 44);
            cardTotal.TabIndex = 15;
            // 
            // lblTotalVal
            // 
            lblTotalVal.BackColor = Color.Transparent;
            lblTotalVal.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblTotalVal.ForeColor = Color.FromArgb(30, 30, 28);
            lblTotalVal.Location = new Point(8, 6);
            lblTotalVal.Name = "lblTotalVal";
            lblTotalVal.Size = new Size(136, 20);
            lblTotalVal.TabIndex = 0;
            lblTotalVal.Text = "14,832";
            // 
            // lblTotalTitle
            // 
            lblTotalTitle.BackColor = Color.Transparent;
            lblTotalTitle.Font = new Font("Segoe UI", 7.5F);
            lblTotalTitle.ForeColor = Color.FromArgb(120, 119, 112);
            lblTotalTitle.Location = new Point(8, 28);
            lblTotalTitle.Name = "lblTotalTitle";
            lblTotalTitle.Size = new Size(136, 12);
            lblTotalTitle.TabIndex = 1;
            lblTotalTitle.Text = "Total rows";
            // 
            // cardExported
            // 
            cardExported.BackColor = Color.White;
            cardExported.BorderStyle = BorderStyle.FixedSingle;
            cardExported.Controls.Add(lblExportedVal);
            cardExported.Controls.Add(lblExportedTitle);
            cardExported.Location = new Point(170, 122);
            cardExported.Name = "cardExported";
            cardExported.Size = new Size(152, 44);
            cardExported.TabIndex = 16;
            // 
            // lblExportedVal
            // 
            lblExportedVal.BackColor = Color.Transparent;
            lblExportedVal.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblExportedVal.ForeColor = Color.FromArgb(30, 30, 28);
            lblExportedVal.Location = new Point(8, 6);
            lblExportedVal.Name = "lblExportedVal";
            lblExportedVal.Size = new Size(136, 20);
            lblExportedVal.TabIndex = 0;
            lblExportedVal.Text = "0";
            // 
            // lblExportedTitle
            // 
            lblExportedTitle.BackColor = Color.Transparent;
            lblExportedTitle.Font = new Font("Segoe UI", 7.5F);
            lblExportedTitle.ForeColor = Color.FromArgb(120, 119, 112);
            lblExportedTitle.Location = new Point(8, 28);
            lblExportedTitle.Name = "lblExportedTitle";
            lblExportedTitle.Size = new Size(136, 12);
            lblExportedTitle.TabIndex = 1;
            lblExportedTitle.Text = "Exported";
            // 
            // cardPct
            // 
            cardPct.BackColor = Color.White;
            cardPct.BorderStyle = BorderStyle.FixedSingle;
            cardPct.Controls.Add(lblPctVal);
            cardPct.Controls.Add(lblPctTitle);
            cardPct.Location = new Point(328, 122);
            cardPct.Name = "cardPct";
            cardPct.Size = new Size(153, 44);
            cardPct.TabIndex = 17;
            // 
            // lblPctVal
            // 
            lblPctVal.BackColor = Color.Transparent;
            lblPctVal.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblPctVal.ForeColor = Color.FromArgb(30, 30, 28);
            lblPctVal.Location = new Point(8, 6);
            lblPctVal.Name = "lblPctVal";
            lblPctVal.Size = new Size(136, 20);
            lblPctVal.TabIndex = 0;
            lblPctVal.Text = "0%";
            // 
            // lblPctTitle
            // 
            lblPctTitle.BackColor = Color.Transparent;
            lblPctTitle.Font = new Font("Segoe UI", 7.5F);
            lblPctTitle.ForeColor = Color.FromArgb(120, 119, 112);
            lblPctTitle.Location = new Point(8, 28);
            lblPctTitle.Name = "lblPctTitle";
            lblPctTitle.Size = new Size(136, 12);
            lblPctTitle.TabIndex = 1;
            lblPctTitle.Text = "Progress";
            // 
            // btnToggle
            // 
            btnToggle.BackColor = Color.FromArgb(24, 95, 165);
            btnToggle.Cursor = Cursors.Hand;
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.FlatStyle = FlatStyle.Flat;
            btnToggle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnToggle.ForeColor = Color.White;
            btnToggle.Location = new Point(12, 183);
            btnToggle.Name = "btnToggle";
            btnToggle.Size = new Size(66, 28);
            btnToggle.TabIndex = 19;
            btnToggle.Text = "▶  Start";
            btnToggle.UseVisualStyleBackColor = false;
            // 
            // btnSettings
            // 
            btnSettings.BackColor = Color.White;
            btnSettings.Cursor = Cursors.Hand;
            btnSettings.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 215);
            btnSettings.FlatStyle = FlatStyle.Flat;
            btnSettings.Font = new Font("Segoe UI", 8.5F);
            btnSettings.ForeColor = Color.FromArgb(30, 30, 28);
            btnSettings.Location = new Point(383, 179);
            btnSettings.Name = "btnSettings";
            btnSettings.Size = new Size(118, 28);
            btnSettings.TabIndex = 20;
            btnSettings.Text = "⚙  Settings";
            btnSettings.UseVisualStyleBackColor = false;
            // 
            // panelStatusBar
            // 
            panelStatusBar.BackColor = Color.FromArgb(24, 95, 165);
            panelStatusBar.Controls.Add(picDot);
            panelStatusBar.Controls.Add(lblStatus);
            panelStatusBar.Controls.Add(lblLastExport);
            panelStatusBar.Location = new Point(0, 217);
            panelStatusBar.Name = "panelStatusBar";
            panelStatusBar.Size = new Size(521, 22);
            panelStatusBar.TabIndex = 21;
            // 
            // picDot
            // 
            picDot.BackColor = Color.Transparent;
            picDot.Location = new Point(12, 7);
            picDot.Name = "picDot";
            picDot.Size = new Size(8, 8);
            picDot.TabIndex = 0;
            picDot.TabStop = false;
            // 
            // lblStatus
            // 
            lblStatus.BackColor = Color.Transparent;
            lblStatus.Font = new Font("Segoe UI", 7.5F);
            lblStatus.ForeColor = Color.FromArgb(220, 220, 220);
            lblStatus.Location = new Point(26, 3);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(260, 16);
            lblStatus.TabIndex = 1;
            lblStatus.Text = "Connected · Source & Output DB";
            // 
            // lblLastExport
            // 
            lblLastExport.BackColor = Color.Transparent;
            lblLastExport.Font = new Font("Segoe UI", 7.5F);
            lblLastExport.ForeColor = Color.FromArgb(220, 220, 220);
            lblLastExport.Location = new Point(355, 2);
            lblLastExport.Name = "lblLastExport";
            lblLastExport.Size = new Size(158, 16);
            lblLastExport.TabIndex = 2;
            lblLastExport.Text = "Last export: --:--:--";
            lblLastExport.TextAlign = ContentAlignment.MiddleRight;
            // 
            // EvDataExporter
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 245, 243);
            ClientSize = new Size(521, 242);
            Controls.Add(lblSource);
            Controls.Add(lblSourceServer);
            Controls.Add(txtSourceServer);
            Controls.Add(lblSourceDb);
            Controls.Add(txtSourceDb);
            Controls.Add(picSourceStatus);
            Controls.Add(this.lblOutputServer);
            Controls.Add(txtOutputServer);
            Controls.Add(this.lblOutputDb);
            Controls.Add(txtOutputDb);
            Controls.Add(picOutputStatus);
            Controls.Add(lblSavePath);
            Controls.Add(txtSavePath);
            Controls.Add(panelDivider);
            Controls.Add(cardTotal);
            Controls.Add(cardExported);
            Controls.Add(cardPct);
            Controls.Add(btnToggle);
            Controls.Add(btnSettings);
            Controls.Add(panelStatusBar);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "EvDataExporter";
            Text = "EvDataExporter";
            ((System.ComponentModel.ISupportInitialize)picSourceStatus).EndInit();
            ((System.ComponentModel.ISupportInitialize)picOutputStatus).EndInit();
            cardTotal.ResumeLayout(false);
            cardExported.ResumeLayout(false);
            cardPct.ResumeLayout(false);
            panelStatusBar.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)picDot).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        // ── Designer fields ──────────────────────────────────────────────
        private NotifyIcon notifyIcon1;

        private Label lblSource, lblSourceServer, lblSourceDb;
        private TextBox txtSourceServer, txtSourceDb;
        private PictureBox picSourceStatus;
        private TextBox txtOutputServer, txtOutputDb;
        private PictureBox picOutputStatus;

        private Label lblSavePath;
        private TextBox txtSavePath;

        private Panel panelDivider;

        private Panel cardTotal, cardExported, cardPct;
        private Label lblTotalVal, lblExportedVal, lblPctVal;
        private Label lblTotalTitle, lblExportedTitle, lblPctTitle;

        private Button btnToggle, btnSettings;

        private Panel panelStatusBar;
        private PictureBox picDot;
        private Label lblStatus, lblLastExport;
    }
}