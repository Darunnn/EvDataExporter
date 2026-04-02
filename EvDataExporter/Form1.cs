using System.Timers;

namespace EvDataExporter
{
    public partial class EvDataExporter : Form
    {
        private Config _config = null!;
        private Databasetocsv? _db;

        private System.Timers.Timer? _timer;
        private bool _running = false;
        private CancellationTokenSource _cts = new();

        // ─────────────────────────────────────────────────────────────────
        public EvDataExporter()
        {
            InitializeComponent();
            HideUnusedControls();
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
            _db?.Dispose();
            _db = null;

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

            Logger.Info($"Config loaded — Server={_config.DbServer}, DB={_config.DbName}, MachineNo={_config.MachineNo}");

            txtSourceServer.Text = _config.DbServer;
            txtSourceDb.Text = _config.DbName;
            txtSavePath.Text = _config.SaveFolder;

            Logger.Info("Testing database connection...");
            _db = new Databasetocsv(_config, picSourceStatus, picDot, lblStatus);
            await _db.TestConnectionAsync();

            if (_db.IsConnected)
                Logger.Info("Database connection OK");
            else
                Logger.Error("Database connection FAILED — ตรวจสอบ config.ini");

            btnToggle.Enabled = _db.IsConnected;
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

            Logger.Info("Export service started — opening connection...");
            try
            {
                await _db!.OpenAsync();
                Logger.Info("Connection opened successfully");
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

        private void HideUnusedControls()
        {
            lblOutput.Visible = false;
            lblOutputServer.Visible = false;
            txtOutputServer.Visible = false;
            lblOutputDb.Visible = false;
            txtOutputDb.Visible = false;
            picOutputStatus.Visible = false;

            lblSource.Text = "DB";
            lblSourceServer.Text = "Server";
            lblSavePath.Text = "Save path";

            txtSourceServer.ReadOnly = true;
            txtSourceDb.ReadOnly = true;
            txtSavePath.ReadOnly = true;
        }
    }
}