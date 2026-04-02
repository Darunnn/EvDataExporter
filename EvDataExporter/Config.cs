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
        /// <summary>
        /// โฟลเดอร์ปลายทาง — ชื่อไฟล์จะถูกสร้างอัตโนมัติโดย DatabaseManager
        /// รูปแบบ: YYYYMMDD_HHMMSS_SeqNo_PrescriptionNo.csv
        /// </summary>
        public string SaveFolder { get; private set; } = "";

        // ── MachineNo filter ─────────────────────────────────────────────
        public string MachineNo { get; private set; } = "11";

        // ── Validation result ────────────────────────────────────────────
        public bool IsValid { get; private set; }
        public List<ConfigError> Errors { get; private set; } = new();

        // ─────────────────────────────────────────────────────────────────
        public Config(string iniPath)
        {
            IniPath = Path.GetFullPath(iniPath);
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// อ่าน config.ini แบบ Key=Value; แล้ว validate ทุก field
        /// ผลลัพธ์อยู่ใน IsValid และ Errors
        /// </summary>
        public void LoadAndValidate()
        {
            Errors.Clear();
            IsValid = false;

            // ── 1. ตรวจว่าไฟล์มีจริง ─────────────────────────────────────
            if (!File.Exists(IniPath))
            {
                Errors.Add(new ConfigError(
                    ConfigSection.File, "IniPath",
                    $"ไม่พบไฟล์ config: {IniPath}"));
                return;
            }

            // ── 2. อ่านทุก line แล้ว parse Key=Value ──────────────────────
            var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in File.ReadAllLines(IniPath, Encoding.UTF8))
            {
                // ข้าม comment และ section header
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                // split ที่ = ตัวแรกเท่านั้น
                int eq = line.IndexOf('=');
                if (eq < 1) continue;

                var key = line[..eq].Trim().TrimEnd(';');
                // ลบ ; ท้าย value ออก (connection string style)
                var val = line[(eq + 1)..].Trim().TrimEnd(';');
                pairs[key] = val;
            }

            // ── 3. Map ค่า ────────────────────────────────────────────────
            DbServer = Get(pairs, "Server");
            DbPort = Get(pairs, "Port", "3306");
            DbName = Get(pairs, "Database");
            DbUser = Get(pairs, "User Id");         // "User Id" ตาม format ใหม่
            DbPassword = Get(pairs, "Password");
            SaveFolder = Get(pairs, "SaveFolder");
            MachineNo = Get(pairs, "MachineNo", "11");

            // ── 4. Validate ───────────────────────────────────────────────
            ValidateRequired(ConfigSection.Database, "Server", DbServer);
            ValidatePort(DbPort);
            ValidateRequired(ConfigSection.Database, "Database", DbName);
            ValidateRequired(ConfigSection.Database, "User Id", DbUser);
            ValidateSaveFolder(SaveFolder);

            IsValid = Errors.Count == 0;
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>สร้าง config.ini ใหม่ถ้ายังไม่มี</summary>
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

        // ─────────────────────────────────────────────────────────────────
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
            ; SaveFolder = โฟลเดอร์ปลายทาง (ชื่อไฟล์สร้างอัตโนมัติ)
            SaveFolder=C:\EV_Data;
            MachineNo=11;
            """;
    }

    // ─────────────────────────────────────────────────────────────────────
    public enum ConfigSection { File, Database, Export }
    public record ConfigError(ConfigSection Section, string Key, string Message);
}