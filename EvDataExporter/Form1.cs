using System.Timers;

namespace EvDataExporter
{
    public partial class EvDataExporter : Form
    {
        private Config _config = null!;
        private Databasetocsv? _db = null;
        private MssqlLookup? _mssqlLookup = null;

        private System.Timers.Timer? _timer;
        private System.Timers.Timer? _healthTimer;   // ← health-check ทุก 10s
        private bool _running = false;
        private bool _autoMode = false;              // ← true = ระบบอยู่ใน auto mode
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
            notifyIcon1.Visible = false;
            Application.Exit();
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
            // ── หยุด health-check เดิมก่อน ───────────────────────────────
            StopHealthTimer();

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

            var saveFolder = string.IsNullOrWhiteSpace(_config.SaveFolder)
                ? _defaultSaveFolder
                : _config.SaveFolder;

            Logger.Info($"Config loaded — MySQL={_config.DbServer}/{_config.DbName} | " +
                        $"MSSQL={_config.MssqlServer}/{_config.MssqlDatabase} | " +
                        $"SaveFolder={saveFolder}");

            // ── แสดงค่าใน UI ──────────────────────────────────────────────
            txtSourceServer.Text = _config.DbServer;
            txtSourceDb.Text = _config.DbName;
            txtOutputServer.Text = _config.MssqlServer;
            txtOutputDb.Text = _config.MssqlDatabase;
            txtSavePath.Text = saveFolder;

            // ── Test connections ──────────────────────────────────────────
            Logger.Info("Testing MySQL connection...");
            _mssqlLookup = new MssqlLookup(_config);
            _db = new Databasetocsv(_config, _mssqlLookup, picSourceStatus, picDot, lblStatus);
            await _db.TestConnectionAsync();

            Logger.Info("Testing MSSQL connection...");
            bool mssqlOk = await _mssqlLookup.TestConnectionAsync();

            var mssqlColor = mssqlOk
                ? Color.FromArgb(52, 199, 89)
                : Color.FromArgb(255, 69, 58);
            PaintDotPublic(picOutputStatus, mssqlColor);

            if (!mssqlOk)
                Logger.Warning("MSSQL connection FAILED — BinNum (field 42) จะเป็นค่าว่าง");

            btnToggle.Enabled = _db.IsConnected;

            // ── Auto-start เมื่อทั้ง 2 DB พร้อม ──────────────────────────
            if (_db.IsConnected && mssqlOk)
            {
                Logger.Info("Both databases connected — auto-starting export service...");
                SetStatus("Both databases OK — auto-starting…");
                _autoMode = true;
                await StartAsync();
            }
            else
            {
                Logger.Info("One or more databases not ready — waiting for connections...");
                SetStatus(_db.IsConnected
                    ? "MySQL OK · MSSQL failed — waiting to reconnect…"
                    : "MySQL failed — waiting to reconnect…");

                // ── เริ่ม health-check เพื่อรอ reconnect ──────────────────
                _autoMode = true;
                StartHealthTimer();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Health Check Timer (ทุก 10 วินาที)
        // ─────────────────────────────────────────────────────────────────
        private void StartHealthTimer()
        {
            if (_healthTimer != null) return;

            _healthTimer = new System.Timers.Timer(10_000);
            _healthTimer.Elapsed += OnHealthCheckElapsed;
            _healthTimer.AutoReset = true;
            _healthTimer.Start();
            Logger.Info("Health-check timer started (interval: 10s)");
        }

        private void StopHealthTimer()
        {
            if (_healthTimer == null) return;
            _healthTimer.Stop();
            _healthTimer.Dispose();
            _healthTimer = null;
            Logger.Info("Health-check timer stopped");
        }

        private async void OnHealthCheckElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!_autoMode) return;

            bool mysqlOk = await CheckMysqlAsync();
            bool mssqlOk = await CheckMssqlAsync();

            // ── อัปเดต UI dot ─────────────────────────────────────────────
            UpdateStatusDots(mysqlOk, mssqlOk);

            if (_running)
            {
                // ── กำลัง run อยู่ → ถ้า DB ใดตัด → auto-stop ────────────
                if (!mysqlOk || !mssqlOk)
                {
                    var who = !mysqlOk ? "MySQL" : "MSSQL";
                    Logger.Warning($"Health-check: {who} disconnected — auto-stopping...");
                    InvokeIfNeeded(() => SetStatus($"{who} disconnected — stopping…"));
                    Stop();
                }
            }
            else
            {
                // ── หยุดอยู่ → ถ้า DB กลับมาครบทั้งคู่ → auto-start ───────
                if (mysqlOk && mssqlOk)
                {
                    Logger.Info("Health-check: Both databases back — auto-restarting...");
                    InvokeIfNeeded(() => SetStatus("Both databases reconnected — restarting…"));

                    // ต้อง re-init เพื่อสร้าง connection object ใหม่
                    await InvokeAsync(async () =>
                    {
                        await ReopenConnectionsAndStartAsync();
                    });
                }
                else
                {
                    var statusMsg = mysqlOk
                        ? "MySQL OK · MSSQL offline — waiting…"
                        : mssqlOk
                            ? "MSSQL OK · MySQL offline — waiting…"
                            : "Both databases offline — waiting…";
                    InvokeIfNeeded(() => SetStatus(statusMsg));
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Ping helpers (ไม่ใช้ connection หลัก — เปิดชั่วคราว)
        // ─────────────────────────────────────────────────────────────────
        private async Task<bool> CheckMysqlAsync()
        {
            try
            {
                using var c = new MySql.Data.MySqlClient.MySqlConnection(
                    _db?.ConnectionString ?? "");
                await c.OpenAsync();
                return c.State == System.Data.ConnectionState.Open;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> CheckMssqlAsync()
        {
            if (_mssqlLookup == null) return false;
            return await _mssqlLookup.TestConnectionAsync();
        }

        private void UpdateStatusDots(bool mysqlOk, bool mssqlOk)
        {
            var green = Color.FromArgb(52, 199, 89);
            var red = Color.FromArgb(255, 69, 58);

            InvokeIfNeeded(() =>
            {
                PaintDotPublic(picSourceStatus, mysqlOk ? green : red);
                PaintDotPublic(picOutputStatus, mssqlOk ? green : red);
                PaintDotPublic(picDot, (mysqlOk && mssqlOk) ? green : red);
            });
        }

        // ─────────────────────────────────────────────────────────────────
        //  Reopen connections แล้ว start (ใช้ตอน reconnect)
        // ─────────────────────────────────────────────────────────────────
        private async Task ReopenConnectionsAndStartAsync()
        {
            try
            {
                // dispose ของเก่า แล้วสร้างใหม่
                _db?.Dispose();
                _mssqlLookup?.Dispose();

                _mssqlLookup = new MssqlLookup(_config);
                _db = new Databasetocsv(
                    _config, _mssqlLookup,
                    picSourceStatus, picDot, lblStatus);

                await StartAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("ReopenConnectionsAndStartAsync failed", ex);
                SetStatus($"Reconnect error: {ex.Message}");
            }
        }

        // ── Helper: Invoke async lambda บน UI thread ─────────────────────
        private Task InvokeAsync(Func<Task> action)
        {
            var tcs = new TaskCompletionSource();
            InvokeIfNeeded(async () =>
            {
                try { await action(); tcs.SetResult(); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        // ── Helper: paint dot จาก Form ───────────────────────────────────
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
                InitialDirectory = Directory.Exists(txtSavePath.Text)
                    ? txtSavePath.Text
                    : AppDomain.CurrentDomain.BaseDirectory
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var selected = dlg.SelectedPath;
                txtSavePath.Text = selected;
                SaveFolderToConfig(selected);
                Logger.Info($"SaveFolder changed to: {selected}");
            }
        }

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
                    var list = lines.ToList();
                    list.Add($"SaveFolder={newFolder};");
                    lines = list.ToArray();
                }

                File.WriteAllLines(_config.IniPath, lines,
                    new System.Text.UTF8Encoding(false));
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
        //  Start / Stop (manual หรือ auto)
        // ─────────────────────────────────────────────────────────────────
        private async void OnToggleClick(object? sender, EventArgs e)
        {
            if (!_running)
            {
                // Manual start → ปิด auto mode เพื่อไม่ให้ health-check บังคับ stop
                _autoMode = false;
                StopHealthTimer();
                await ReopenConnectionsAndStartAsync();
            }
            else
            {
                // Manual stop → ปิด auto mode ด้วย
                _autoMode = false;
                StopHealthTimer();
                Stop();
            }
        }

        private async Task StartAsync()
        {
            _running = true;
            _cts = new CancellationTokenSource();
            InvokeIfNeeded(() =>
            {
                btnToggle.Text = "⏹  Stop";
                btnToggle.BackColor = Color.FromArgb(200, 60, 50);
            });

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
                InvokeIfNeeded(() => SetStatus($"Error: {ex.Message}"));
                Stop();
                return;
            }

            await RunExportAsync();

            _timer = new System.Timers.Timer(30_000);
            _timer.Elapsed += async (_, _) => { if (_running) await RunExportAsync(); };
            _timer.AutoReset = true;
            _timer.Start();
            Logger.Info("Export timer started (interval: 30s)");

            // ── เริ่ม health-check เฉพาะ auto mode ───────────────────────
            if (_autoMode) StartHealthTimer();
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
                SetStatus(_autoMode ? "DB disconnected — waiting to reconnect…" : "Stopped");
            });

            // ── ถ้า auto mode → health-check ยังทำงานเพื่อรอ reconnect ───
            if (_autoMode) StartHealthTimer();
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

                // หยุดทุกอย่างก่อนเปิด settings
                _autoMode = false;
                StopHealthTimer();
                if (_running) Stop();

                var proc = System.Diagnostics.Process.Start("notepad.exe", _config.IniPath);
                if (proc != null) await proc.WaitForExitAsync();

                Logger.Info("Notepad closed — reloading config...");
                await InitConfigAndConnectAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Settings error", ex);
                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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