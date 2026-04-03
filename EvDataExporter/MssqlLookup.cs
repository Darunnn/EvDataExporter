using Microsoft.Data.SqlClient;

namespace EvDataExporter
{
    /// <summary>
    /// MSSQL Lookup — ใช้สำหรับดึง ln_CassetteNo เพื่อใส่ใน BinNum (field 42) เท่านั้น
    ///
    /// Logic (ตาม Data Dictionary field 42):
    ///   WHERE vc_DrugCd = f_orderitemcode
    ///   → SELECT ln_CassetteNo
    ///
    /// Cache ผลลัพธ์ไว้ใน Dictionary เพื่อไม่ query ซ้ำสำหรับยาเดิม
    /// </summary>
    public class MssqlLookup : IDisposable
    {
        private SqlConnection? _conn;

        public bool IsConnected { get; private set; }

        // ── Cache: DrugCd → CassetteNo ───────────────────────────────────
        private readonly Dictionary<string, string> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly string _connectionString;
        private readonly string _mssqlDatabase;

        // ─────────────────────────────────────────────────────────────────
        public MssqlLookup(Config config)
        {
            if (!config.IsValid)
                throw new InvalidOperationException("Config ยังไม่ผ่าน validation");

            _mssqlDatabase = config.MssqlDatabase;

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = config.MssqlServer,
                InitialCatalog = config.MssqlDatabase,
                ConnectTimeout = 5,
                TrustServerCertificate = true
            };

            if (config.MssqlTrustedConnection)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = config.MssqlUser;
                builder.Password = config.MssqlPassword;
            }

            _connectionString = builder.ConnectionString;
            Logger.Info($"MssqlLookup created — Server={config.MssqlServer}, DB={config.MssqlDatabase}");
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>ทดสอบ connection และอัปเดต IsConnected</summary>
        public async Task<bool> TestConnectionAsync()
        {
            Logger.Info("MssqlLookup.TestConnectionAsync — start");
            try
            {
                using var c = new SqlConnection(_connectionString);
                await c.OpenAsync();
                IsConnected = c.State == System.Data.ConnectionState.Open;
                Logger.Info("MssqlLookup.TestConnectionAsync — success");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Logger.Error("MssqlLookup.TestConnectionAsync — failed", ex);
            }
            return IsConnected;
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>เปิด connection สำหรับใช้งานต่อเนื่องระหว่าง export cycle</summary>
        public async Task OpenAsync()
        {
            Logger.Info("MssqlLookup.OpenAsync — opening SQL Server connection...");
            _conn = new SqlConnection(_connectionString);
            await _conn.OpenAsync();
            Logger.Info("MssqlLookup.OpenAsync — connection opened");
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// ดึง ln_CassetteNo จาก MSSQL โดย WHERE vc_DrugCd = drugCd
        /// ถ้าไม่พบ หรือ connection ไม่พร้อม → คืน ""
        /// ผลลัพธ์ถูก cache ไว้ตลอด session
        /// </summary>
        public async Task<string> GetCassetteNoAsync(string drugCd)
        {
            if (string.IsNullOrWhiteSpace(drugCd)) return "";

            // ── Cache hit ────────────────────────────────────────────────
            if (_cache.TryGetValue(drugCd, out var cached))
                return cached;

            // ── ถ้า connection ไม่พร้อมให้คืน "" ไม่หยุด export ─────────
            if (_conn is null || _conn.State != System.Data.ConnectionState.Open)
            {
                Logger.Warning($"MssqlLookup.GetCassetteNoAsync — connection not open, skip DrugCd={drugCd}");
                return "";
            }

            try
            {
                using var cmd = _conn.CreateCommand();
                // ปรับ table name ตามจริงของ MSSQL database
                cmd.CommandText =
                    $"SELECT TOP 1 ln_CassetteNo " +
                    $"FROM [{_mssqlDatabase}].[dbo].[tb_drug_cassette] " +
                    $"WHERE vc_DrugCd = @drugCd";
                cmd.Parameters.AddWithValue("@drugCd", drugCd);

                var result = await cmd.ExecuteScalarAsync();
                var cassetteNo = result is null || result == DBNull.Value
                    ? ""
                    : result.ToString() ?? "";

                _cache[drugCd] = cassetteNo;
                Logger.Info($"MssqlLookup — DrugCd={drugCd} → CassetteNo={cassetteNo}");
                return cassetteNo;
            }
            catch (Exception ex)
            {
                Logger.Error($"MssqlLookup.GetCassetteNoAsync — failed for DrugCd={drugCd}", ex);
                _cache[drugCd] = "";   // cache miss เพื่อไม่ retry ซ้ำ
                return "";
            }
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Pre-load CassetteNo ทีเดียวสำหรับทุก DrugCd ใน batch
        /// ลด round-trip จาก N ครั้ง เหลือ 1 ครั้ง
        /// </summary>
        public async Task PrefetchAsync(IEnumerable<string> drugCds)
        {
            if (_conn is null || _conn.State != System.Data.ConnectionState.Open)
                return;

            // กรองเฉพาะที่ยังไม่มีใน cache
            var toFetch = drugCds
                .Where(d => !string.IsNullOrWhiteSpace(d) && !_cache.ContainsKey(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (toFetch.Count == 0) return;

            Logger.Info($"MssqlLookup.PrefetchAsync — prefetching {toFetch.Count} DrugCd(s)...");

            try
            {
                var inList = string.Join(",",
                    toFetch.Select(d => $"'{d.Replace("'", "''")}'"));

                using var cmd = _conn.CreateCommand();
                cmd.CommandText =
                    $"SELECT vc_DrugCd, ln_CassetteNo " +
                    $"FROM [{_mssqlDatabase}].[dbo].[tb_drug_cassette] " +
                    $"WHERE vc_DrugCd IN ({inList})";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var drugCd = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    var cassetteNo = reader.IsDBNull(1) ? "" : reader.GetValue(1).ToString() ?? "";
                    if (!string.IsNullOrEmpty(drugCd))
                        _cache[drugCd] = cassetteNo;
                }

                // DrugCd ที่ไม่พบใน DB ก็ cache เป็น "" เพื่อไม่ query ซ้ำ
                foreach (var d in toFetch.Where(d => !_cache.ContainsKey(d)))
                    _cache[d] = "";

                Logger.Info($"MssqlLookup.PrefetchAsync — done, {_cache.Count} entries cached");
            }
            catch (Exception ex)
            {
                Logger.Error("MssqlLookup.PrefetchAsync — failed", ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        public void ClearCache() => _cache.Clear();

        public void Dispose()
        {
            Logger.Info("MssqlLookup disposed");
            _conn?.Dispose();
        }
    }
}