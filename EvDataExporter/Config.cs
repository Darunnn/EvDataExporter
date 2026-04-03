using System.Text;

namespace EvDataExporter
{
    /// <summary>
    /// อ่านและตรวจสอบความถูกต้องของ config.ini
    /// รองรับ format: Key=Value; (connection string style)
    ///
    /// MySQL  → Source หลัก (tb_thaneshos_middle)
    /// MSSQL  → Lookup เพิ่มเติม เพื่อดึง ln_CassetteNo → BinNum (field 42)
    /// </summary>
    public class Config
    {
        // ── Path ─────────────────────────────────────────────────────────
        public string IniPath { get; }

        // ── MySQL config ─────────────────────────────────────────────────
        public string DbServer { get; private set; } = "";
        public string DbPort { get; private set; } = "3306";
        public string DbName { get; private set; } = "";
        public string DbUser { get; private set; } = "";
        public string DbPassword { get; private set; } = "";

        // ── MSSQL Lookup config ──────────────────────────────────────────
        public string MssqlServer { get; private set; } = "";
        public string MssqlDatabase { get; private set; } = "";
        public string MssqlUser { get; private set; } = "";
        public string MssqlPassword { get; private set; } = "";
        public bool MssqlTrustedConnection { get; private set; } = false;

        // ── Export config ────────────────────────────────────────────────
        public string SaveFolder { get; private set; } = "";

        // ── Default SaveFolder = [exe]\Exportcsv ─────────────────────────
        public static string DefaultSaveFolder =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exportcsv");

        // ── Validation ───────────────────────────────────────────────────
        public bool IsValid { get; private set; }
        public List<ConfigError> Errors { get; private set; } = new();

        // ─────────────────────────────────────────────────────────────────
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

            // ── MySQL ────────────────────────────────────────────────────
            DbServer = Get(pairs, "Server");
            DbPort = Get(pairs, "Port", "3306");
            DbName = Get(pairs, "Database");
            DbUser = Get(pairs, "User Id");
            DbPassword = Get(pairs, "Password");

            // ── MSSQL Lookup ─────────────────────────────────────────────
            MssqlServer = Get(pairs, "MssqlServer");
            MssqlDatabase = Get(pairs, "MssqlDatabase");
            MssqlUser = Get(pairs, "MssqlUser");
            MssqlPassword = Get(pairs, "MssqlPassword");
            MssqlTrustedConnection = Get(pairs, "MssqlTrustedConnection")
                                        .Equals("true", StringComparison.OrdinalIgnoreCase);

            // ── Export: ถ้า SaveFolder ว่างใน config → ใช้ default ───────
            var rawSaveFolder = Get(pairs, "SaveFolder");
            SaveFolder = string.IsNullOrWhiteSpace(rawSaveFolder)
                ? DefaultSaveFolder
                : rawSaveFolder;

            // ── Validate MySQL (required) ────────────────────────────────
            ValidateRequired(ConfigSection.Database, "Server", DbServer);
            ValidatePort(DbPort);
            ValidateRequired(ConfigSection.Database, "Database", DbName);
            ValidateRequired(ConfigSection.Database, "User Id", DbUser);

            // ── Validate MSSQL (required) ────────────────────────────────
            ValidateRequired(ConfigSection.MssqlLookup, "MssqlServer", MssqlServer);
            ValidateRequired(ConfigSection.MssqlLookup, "MssqlDatabase", MssqlDatabase);
            if (!MssqlTrustedConnection)
                ValidateRequired(ConfigSection.MssqlLookup, "MssqlUser", MssqlUser);

            // ── Validate Export ──────────────────────────────────────────
            // SaveFolder มีค่า default เสมอ จึง validate path chars เท่านั้น
            ValidateSaveFolderChars(SaveFolder);

            IsValid = Errors.Count == 0;
        }

        // ─────────────────────────────────────────────────────────────────
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

        // ── Validation helpers ────────────────────────────────────────────
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

        /// <summary>ตรวจเฉพาะอักขระผิด — ไม่บังคับให้ตั้งค่า (มี default แล้ว)</summary>
        private void ValidateSaveFolderChars(string folder)
        {
            char[] invalid = Path.GetInvalidPathChars();
            if (folder.Any(c => invalid.Contains(c)))
                Errors.Add(new ConfigError(
                    ConfigSection.Export, "SaveFolder",
                    "SaveFolder มีอักขระที่ไม่อนุญาต"));
        }

        private static string Get(
            Dictionary<string, string> d, string key, string def = "")
            => d.TryGetValue(key, out var v) ? v : def;

        // ─────────────────────────────────────────────────────────────────
        private string DefaultTemplate() => $"""
            ; EV Data Exporter - config.ini

            ; ── MySQL Source ────────────────────────────────────────────────────────────
            Server=103.99.11.97;
            Database=db_thanes_system_pattaya;
            User Id=thanes1;
            Password=@Thanes1234;

            ; ── MSSQL Lookup (ดึง ln_CassetteNo → BinNum field 42) ───────────────────
            MssqlServer=192.168.1.10\SQLEXPRESS;
            MssqlDatabase=EV_Production_DB;
            MssqlUser=sa;
            MssqlPassword=YourMssqlPassword;
            ; MssqlTrustedConnection=true

            ; ── Export settings ──────────────────────────────────────────────────────
            ; ถ้าไม่ตั้งค่า SaveFolder จะใช้ {DefaultSaveFolder} เป็น default
            SaveFolder={DefaultSaveFolder};
            """;
    }

    // ─────────────────────────────────────────────────────────────────────
    public enum ConfigSection { File, Database, MssqlLookup, Export }
    public record ConfigError(ConfigSection Section, string Key, string Message);
}