using System.Text;

namespace EvDataExporter
{
    /// <summary>
    /// Logger แบบ thread-safe
    /// โครงสร้าง:
    ///   [exe]\log\YYYYMMDD.log          ← info / warning ทั่วไป
    ///   [exe]\log\error\YYYYMMDD.log    ← error เท่านั้น
    /// </summary>
    public static class Logger
    {
        // ── Base log folder = [exe]\log ───────────────────────────────────
        private static readonly string _logDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");

        private static readonly string _errDir =
            Path.Combine(_logDir, "error");

        private static readonly object _lock = new();

        // ─────────────────────────────────────────────────────────────────
        public static void Info(string message) => Write(LogLevel.INFO, message);
        public static void Warning(string message) => Write(LogLevel.WARN, message);
        public static void Error(string message, Exception? ex = null)
        {
            var full = ex is null ? message
                : $"{message}\n  Exception : {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}";
            Write(LogLevel.ERROR, full);
        }

        // ─────────────────────────────────────────────────────────────────
        private static void Write(LogLevel level, string message)
        {
            var now = DateTime.Now;
            var date = now.ToString("yyyyMMdd");
            var time = now.ToString("HH:mm:ss.fff");
            var line = $"[{time}] [{level,-5}] {message}";

            lock (_lock)
            {
                // ── เขียน log หลัก (ทุก level) ───────────────────────────
                WriteFile(_logDir, date, line);

                // ── เขียน error log (เฉพาะ ERROR) ────────────────────────
                if (level == LogLevel.ERROR)
                    WriteFile(_errDir, date, line);
            }
        }

        private static void WriteFile(string dir, string date, string line)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{date}.log");
                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // ถ้าเขียน log ไม่ได้ → ไม่ throw ออกไปรบกวน flow หลัก
            }
        }

        private enum LogLevel { INFO, WARN, ERROR }
    }
}