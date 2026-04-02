using System.Runtime.InteropServices;
using System.Text;

namespace EvDataExporter
{
    /// <summary>
    /// อ่านและตรวจสอบความถูกต้องของ config.ini
    /// แยกออกจาก DatabaseManager โดยสมบูรณ์
    /// </summary>
    public class Config
    {
        // ── Windows API อ่าน .ini ────────────────────────────────────────
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(
            string section, string key, string defaultVal,
            StringBuilder result, int size, string filePath);

        private const int BUF = 512;

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
        /// อ่าน config.ini แล้ว validate ทุก field
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
                    ConfigSection.File,
                    "IniPath",
                    $"ไม่พบไฟล์ config: {IniPath}"));
                return;
            }

            // ── 2. อ่านค่า ───────────────────────────────────────────────
            DbServer = Ini("Database", "Server", "");
            DbPort = Ini("Database", "Port", "3306");
            DbName = Ini("Database", "Database", "");
            DbUser = Ini("Database", "Username", "");
            DbPassword = Ini("Database", "Password", "");
            SaveFolder = Ini("Export", "SaveFolder", "");

            // ── 3. validate แต่ละ field ──────────────────────────────────
            ValidateRequired(ConfigSection.Database, "Server", DbServer);
            ValidatePort(DbPort);
            ValidateRequired(ConfigSection.Database, "Database", DbName);
            ValidateRequired(ConfigSection.Database, "Username", DbUser);
            ValidateSaveFolder(SaveFolder);

            IsValid = Errors.Count == 0;
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>สร้าง config.ini ใหม่ถ้ายังไม่มี</summary>
        public void CreateDefault()
        {
            if (File.Exists(IniPath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(IniPath) ?? ".");
            File.WriteAllText(IniPath, DefaultTemplate(), Encoding.UTF8);
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>แสดง error ทั้งหมดเป็น string สำหรับ MessageBox</summary>
        public string ErrorSummary()
        {
            if (IsValid) return "Config ถูกต้องทุก field";
            return string.Join(Environment.NewLine,
                Errors.Select(e => $"[{e.Section}] {e.Key}: {e.Message}"));
        }

        // ─────────────────────────────────────────────────────────────────
        //  Validators
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

            // ตรวจอักขระต้องห้ามใน path
            char[] invalid = Path.GetInvalidPathChars();
            if (folder.Any(c => invalid.Contains(c)))
                Errors.Add(new ConfigError(
                    ConfigSection.Export, "SaveFolder",
                    "SaveFolder มีอักขระที่ไม่อนุญาต"));
        }

        // ─────────────────────────────────────────────────────────────────
        private string Ini(string sec, string key, string def)
        {
            var sb = new StringBuilder(BUF);
            GetPrivateProfileString(sec, key, def, sb, BUF, IniPath);
            return sb.ToString().Trim();
        }

        private static string DefaultTemplate() => """
            [Database]
            Server=192.168.1.10
            Port=3306
            Database=EV_Production_DB
            Username=ev_user
            Password=ev_pass123

            [Export]
            ; โฟลเดอร์ปลายทาง — ชื่อไฟล์สร้างอัตโนมัติ: YYYYMMDD_HHMMSS_SeqNo_PrescriptionNo.csv
            SaveFolder=C:\EV_Data
            """;
    }

    // ─────────────────────────────────────────────────────────────────────
    public enum ConfigSection { File, Database, Export }

    public record ConfigError(ConfigSection Section, string Key, string Message);
}