using System.Text;

namespace EvDataExporter
{
    /// <summary>
    /// อ่านและตรวจสอบความถูกต้องของ config.ini
    /// รองรับ format: Key=Value; (connection string style)
    /// </summary>
    public class Config
    {
        // ── Path ─────────────────────────────────────────────────────────
        public string IniPath { get; }

        // ── Config values (อ่านได้หลัง LoadAndValidate()) ───────────────
        public string DbServer { get; private set; } = "";
        public string DbPort { get; private set; } = "3306";
        public string DbName { get; private set; } = "";
        public string DbUser { get; private set; } = "";
        public string DbPassword { get; private set; } = "";
        public string SaveFolder { get; private set; } = "";
        public string MachineNo { get; private set; } = "11";

        // ── Validation result ────────────────────────────────────────────
        public bool IsValid { get; private set; }
        public List<ConfigError> Errors { get; private set; } = new();

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// ถ้าไม่ส่ง path → ใช้ [exe folder]\connectdatabase\config.ini
        /// </summary>
        public Config(string? iniPath = null)
        {
            iniPath ??= Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "connectdatabase",
                "config.ini");
            IniPath = Path.GetFullPath(iniPath);
        }

        // ─────────────────────────────────────────────────────────────────
        public void LoadAndValidate()
        {
            Errors.Clear();
            IsValid = false;

            if (!File.Exists(IniPath))
            {
                Errors.Add(new ConfigError(
                    ConfigSection.File, "IniPath",
                    $"ไม่พบไฟล์ config: {IniPath}"));
                return;
            }

            var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in File.ReadAllLines(IniPath, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) ||
                    line.StartsWith(";") || line.StartsWith("#")) continue;

                int eq = line.IndexOf('=');
                if (eq < 1) continue;

                var key = line[..eq].Trim().TrimEnd(';');
                var val = line[(eq + 1)..].Trim().TrimEnd(';');
                pairs[key] = val;
            }

            DbServer = Get(pairs, "Server");
            DbPort = Get(pairs, "Port", "3306");
            DbName = Get(pairs, "Database");
            DbUser = Get(pairs, "User Id");
            DbPassword = Get(pairs, "Password");
            SaveFolder = Get(pairs, "SaveFolder");
            MachineNo = Get(pairs, "MachineNo", "11");

            ValidateRequired(ConfigSection.Database, "Server", DbServer);
            ValidatePort(DbPort);
            ValidateRequired(ConfigSection.Database, "Database", DbName);
            ValidateRequired(ConfigSection.Database, "User Id", DbUser);
            ValidateSaveFolder(SaveFolder);

            IsValid = Errors.Count == 0;
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>สร้าง config.ini พร้อมโฟลเดอร์ถ้ายังไม่มี</summary>
        public void CreateDefault()
        {
            if (File.Exists(IniPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(IniPath) ?? ".");
            File.WriteAllText(IniPath, DefaultTemplate(), new UTF8Encoding(false));
        }

        // ─────────────────────────────────────────────────────────────────
        public string ErrorSummary()
        {
            if (IsValid) return "Config ถูกต้องทุก field";
            return string.Join(Environment.NewLine,
                Errors.Select(e => $"[{e.Section}] {e.Key}: {e.Message}"));
        }

        // ─────────────────────────────────────────────────────────────────
        private void ValidateRequired(ConfigSection sec, string key, string val)
        {
            if (string.IsNullOrWhiteSpace(val))
                Errors.Add(new ConfigError(sec, key, $"ต้องระบุค่า {key}"));
        }

        private void ValidatePort(string portStr)
        {
            if (!int.TryParse(portStr, out int port) || port < 1 || port > 65535)
                Errors.Add(new ConfigError(
                    ConfigSection.Database, "Port",
                    $"Port '{portStr}' ไม่ถูกต้อง (1–65535)"));
        }

        private void ValidateSaveFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                Errors.Add(new ConfigError(
                    ConfigSection.Export, "SaveFolder", "ต้องระบุ SaveFolder"));
                return;
            }
            char[] invalid = Path.GetInvalidPathChars();
            if (folder.Any(c => invalid.Contains(c)))
                Errors.Add(new ConfigError(
                    ConfigSection.Export, "SaveFolder",
                    "SaveFolder มีอักขระที่ไม่อนุญาต"));
        }

        private static string Get(
            Dictionary<string, string> d, string key, string def = "")
            => d.TryGetValue(key, out var v) ? v : def;

        private static string DefaultTemplate() => """
            ; EV Data Exporter - config.ini
            ; Database connection (MySQL)
            Server=103.99.11.97;
            Database=db_thanes_system_pattaya;
            User Id=thanes1;
            Password=@Thanes1234;

            ; Export settings
            SaveFolder=C:\EV_Data;
            MachineNo=11;
            """;
    }

    // ─────────────────────────────────────────────────────────────────────
    public enum ConfigSection { File, Database, Export }
    public record ConfigError(ConfigSection Section, string Key, string Message);
}