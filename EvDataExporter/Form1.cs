using System.Timers;

namespace EvDataExporter
{
    public partial class EvDataExporter : Form
    {
        private Config _config = null!;
        private Databasetocsv? _db = null;
        private MssqlLookup? _mssqlLookup = null;

        private System.Timers.Timer? _timer;
        private bool _running = false;
        private CancellationTokenSource _cts = new();

        // ── Default SaveFolder = [exe]\Exportcsv ─────────────────────────
        private static readonly string _defaultSaveFolder =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exportcsv");

        // ─────────────────────────────────────────────────────────────────
        public EvDataExporter()
        {
            InitializeComponent();
            InitializeTray();

            Load += OnFormLoad;
            btnToggle.Click += OnToggleClick;
            btnSettings.Click += OnSettingsClick;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Tray
        // ─────────────────────────────────────────────────────────────────
        private void InitializeTray()
        {
            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("เปิดหน้าต่าง", null, OnOpen);
            trayMenu.Items.Add("ออกจากโปรแกรม", null, OnExit);
            notifyIcon1.Text = "EvDataExporter";
            notifyIcon1.ContextMenuStrip = trayMenu;
            notifyIcon1.DoubleClick += OnOpen;
            notifyIcon1.Visible = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
                notifyIcon1.Visible = true;
            }
        }

        private void OnOpen(object? sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            notifyIcon1.Visible = false;
            BringToFront();
        }

        private void OnExit(object? sender, EventArgs e)
        {
            Logger.Info("Application exit by user");
            Stop();
            notifyIcon1.Visible = false;
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(2000, "EvDataExporter",
                    "โปรแกรมยังทำงานอยู่ใน system tray", ToolTipIcon.Info);
                Logger.Info("Minimized to system tray");
            }
            else
            {
                Logger.Info("Application closing");
                Stop();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Form Load
        // ─────────────────────────────────────────────────────────────────
        private async void OnFormLoad(object? sender, EventArgs e)
        {
            Logger.Info("=== EvDataExporter started ===");
            await InitConfigAndConnectAsync();
        }

        private async Task InitConfigAndConnectAsync()
        {
            // ── ทำความสะอาด instance เดิม ─────────────────────────────────
            _db?.Dispose();
            _db = null;
            _mssqlLookup?.Dispose();
            _mssqlLookup = null;

            // ── Load config ───────────────────────────────────────────────
            Logger.Info("Loading config...");
            _config = new Config();
            _config.CreateDefault();
            _config.LoadAndValidate();

            // ── ถ้า SaveFolder ว่าง → ใช้ default [exe]\Exportcsv ─────────
            // (เฉพาะตอนที่ config valid แต่ SaveFolder ไม่ได้ตั้ง ให้ fallback)
            // เนื่องจาก Config.LoadAndValidate จะ error ถ้า SaveFolder ว่าง
            // จึง patch ก่อน validate ไม่ได้ — ใช้วิธีสร้าง default template
            // ที่มีค่า SaveFolder อยู่แล้ว แต่ถ้า user ลบออกไป ให้แสดง path default
            // ใน UI เท่านั้น (ไม่บังคับเขียน config)

            if (!_config.IsValid)
            {
                var msg = _config.ErrorSummary();
                Logger.Error($"Config validation failed: {msg}");
                SetStatus($"Config error — {msg}");
                MessageBox.Show(msg, "Config Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnToggle.Enabled = false;
                return;
            }

            // ── ถ้า SaveFolder ในconfig ว่าง ใช้ค่า default ───────────────
            // (กรณีนี้จะไม่เกิดเพราะ Validate บังคับ แต่เผื่อมีการแก้ Validate)
            var saveFolder = string.IsNullOrWhiteSpace(_config.SaveFolder)
                ? _defaultSaveFolder
                : _config.SaveFolder;

            Logger.Info($"Config loaded — MySQL={_config.DbServer}/{_config.DbName} | " +
                        $"MSSQL={_config.MssqlServer}/{_config.MssqlDatabase} | " +
                        $"SaveFolder={saveFolder}");

            // ── แสดงค่าใน UI ──────────────────────────────────────────────
            txtSourceServer.Text = _config.DbServer;       // MySQL server
            txtSourceDb.Text = _config.DbName;             // MySQL database
            txtOutputServer.Text = _config.MssqlServer;    // MSSQL server
            txtOutputDb.Text = _config.MssqlDatabase;      // MSSQL database
            txtSavePath.Text = saveFolder;

            // ── Test MySQL connection ─────────────────────────────────────
            Logger.Info("Testing MySQL connection...");
            _mssqlLookup = new MssqlLookup(_config);
            _db = new Databasetocsv(_config, _mssqlLookup, picSourceStatus, picDot, lblStatus);
            await _db.TestConnectionAsync();

            // ── Test MSSQL connection ─────────────────────────────────────
            Logger.Info("Testing MSSQL connection...");
            bool mssqlOk = await _mssqlLookup.TestConnectionAsync();

            // ── แสดงสถานะ MSSQL dot ───────────────────────────────────────
            var mssqlColor = mssqlOk
                ? Color.FromArgb(52, 199, 89)
                : Color.FromArgb(255, 69, 58);
            PaintDotPublic(picOutputStatus, mssqlColor);

            if (!mssqlOk)
                Logger.Warning("MSSQL connection FAILED — BinNum (field 42) จะเป็นค่าว่าง");

            // ── Enable Start เมื่อ MySQL พร้อม (MSSQL แค่ warn ไม่บล็อก) ──
            btnToggle.Enabled = _db.IsConnected;

            if (_db.IsConnected)
                Logger.Info("MySQL OK — ready to export");
            else
                Logger.Error("MySQL FAILED — ตรวจสอบ config.ini");
        }

        // ── Helper: paint dot จาก Form (ไม่ผ่าน Databasetocsv) ──────────
        private static void PaintDotPublic(PictureBox pic, Color color)
        {
            if (pic.InvokeRequired) { pic.Invoke(() => PaintDotPublic(pic, color)); return; }
            var bmp = new Bitmap(pic.Width, pic.Height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var br = new SolidBrush(color);
            g.FillEllipse(br, 0, 0, pic.Width - 1, pic.Height - 1);
            pic.Image = bmp;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Browse folder
        // ─────────────────────────────────────────────────────────────────
        private void OnBrowseClick(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "เลือก folder สำหรับเก็บไฟล์ CSV",
                UseDescriptionForTitle = true,
                // เปิด dialog ที่ path ปัจจุบันก่อน (ถ้ามี)
                InitialDirectory = Directory.Exists(txtSavePath.Text)
                    ? txtSavePath.Text
                    : AppDomain.CurrentDomain.BaseDirectory
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var selected = dlg.SelectedPath;
                txtSavePath.Text = selected;

                // ── อัปเดต SaveFolder ใน config และ Databasetocsv ──────────
                // เขียนลง config.ini ทันทีเพื่อให้ถาวร
                SaveFolderToConfig(selected);

                Logger.Info($"SaveFolder changed to: {selected}");
            }
        }

        /// <summary>
        /// เขียน SaveFolder ที่ user เลือกลง config.ini
        /// (แทนที่บรรทัด SaveFolder= เดิม)
        /// </summary>
        private void SaveFolderToConfig(string newFolder)
        {
            try
            {
                if (!File.Exists(_config.IniPath)) return;

                var lines = File.ReadAllLines(_config.IniPath, System.Text.Encoding.UTF8);
                bool found = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("SaveFolder", StringComparison.OrdinalIgnoreCase)
                        && trimmed.Contains('='))
                    {
                        lines[i] = $"SaveFolder={newFolder};";
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // ถ้าไม่มีบรรทัด SaveFolder เลย → append ต่อท้าย
                    var list = lines.ToList();
                    list.Add($"SaveFolder={newFolder};");
                    lines = list.ToArray();
                }

                File.WriteAllLines(_config.IniPath, lines, new System.Text.UTF8Encoding(false));

                // reload config เพื่ออัปเดต _config.SaveFolder
                _config.LoadAndValidate();

                Logger.Info($"config.ini updated — SaveFolder={newFolder}");
            }
            catch (Exception ex)
            {
                Logger.Error("SaveFolderToConfig failed", ex);
                MessageBox.Show($"ไม่สามารถบันทึก SaveFolder ได้\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Start / Stop
        // ─────────────────────────────────────────────────────────────────
        private async void OnToggleClick(object? sender, EventArgs e)
        {
            if (!_running) await StartAsync();
            else Stop();
        }

        private async Task StartAsync()
        {
            _running = true;
            _cts = new CancellationTokenSource();
            btnToggle.Text = "⏹  Stop";
            btnToggle.BackColor = Color.FromArgb(200, 60, 50);

            Logger.Info("Export service started — opening connections...");
            try
            {
                await _db!.OpenAsync();
                Logger.Info("MySQL connection opened");

                await _mssqlLookup!.OpenAsync();
                Logger.Info("MSSQL Lookup connection opened");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open connection on Start", ex);
                SetStatus($"Error: {ex.Message}");
                Stop();
                return;
            }

            await RunExportAsync();

            _timer = new System.Timers.Timer(30_000);
            _timer.Elapsed += async (_, _) => { if (_running) await RunExportAsync(); };
            _timer.AutoReset = true;
            _timer.Start();
            Logger.Info("Export timer started (interval: 30s)");
        }

        private void Stop()
        {
            _running = false;
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            _cts.Cancel();

            Logger.Info("Export service stopped");
            InvokeIfNeeded(() =>
            {
                btnToggle.Text = "▶  Start";
                btnToggle.BackColor = Color.FromArgb(24, 95, 165);
                SetStatus("Stopped");
            });
        }

        // ─────────────────────────────────────────────────────────────────
        //  Export
        // ─────────────────────────────────────────────────────────────────
        private async Task RunExportAsync()
        {
            if (_db is null) return;

            Logger.Info("--- Export cycle begin ---");
            SetStatus("Exporting…");

            try
            {
                var progress = new Progress<(int exported, int total)>(p =>
                {
                    InvokeIfNeeded(() =>
                    {
                        lblExportedVal.Text = p.exported.ToString("N0");
                        lblTotalVal.Text = p.total.ToString("N0");
                        lblPctVal.Text = p.total > 0
                            ? $"{p.exported * 100 / p.total}%"
                            : "0%";
                    });
                    Logger.Info($"Progress: {p.exported}/{p.total}");
                });

                int count = await _db.ExportCsvAsync(progress);

                InvokeIfNeeded(() =>
                {
                    lblLastExport.Text = $"Last export: {DateTime.Now:HH:mm:ss}";
                    SetStatus(count == 0 ? "No new records" : $"Exported {count} file(s) OK");
                });

                Logger.Info($"Export cycle done — {count} file(s) exported");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Export cancelled by Stop()");
            }
            catch (Exception ex)
            {
                Logger.Error("Export cycle failed", ex);
                InvokeIfNeeded(() => SetStatus($"Error: {ex.Message}"));
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Settings
        // ─────────────────────────────────────────────────────────────────
        private async void OnSettingsClick(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("Opening config.ini in Notepad...");
                if (_running) Stop();

                var proc = System.Diagnostics.Process.Start("notepad.exe", _config.IniPath);
                if (proc != null) await proc.WaitForExitAsync();

                Logger.Info("Notepad closed — reloading config...");
                await InitConfigAndConnectAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Settings error", ex);
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────
        private void SetStatus(string msg) =>
            InvokeIfNeeded(() => lblStatus.Text = msg);

        private void InvokeIfNeeded(Action a)
        {
            if (InvokeRequired) Invoke(a);
            else a();
        }
    }
}