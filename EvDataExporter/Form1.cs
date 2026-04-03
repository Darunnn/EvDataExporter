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

            Logger.Info($"Config loaded — MySQL={_config.DbServer}/{_config.DbName} | " +
                        $"MSSQL={_config.MssqlServer}/{_config.MssqlDatabase} | MachineNo=11 (hardcoded)");

            // ── แสดงค่าใน UI ──────────────────────────────────────────────
            txtSourceServer.Text = _config.DbServer;
            txtSourceDb.Text = _config.DbName;
            txtOutputServer.Text = _config.MssqlServer;   // แสดง MSSQL server
            txtOutputDb.Text = _config.MssqlDatabase; // แสดง MSSQL database
            txtSavePath.Text = _config.SaveFolder;

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
                // MySQL
                await _db!.OpenAsync();
                Logger.Info("MySQL connection opened");

                // MSSQL Lookup
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