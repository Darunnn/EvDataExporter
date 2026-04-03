using MySql.Data.MySqlClient;
using System.Text;

namespace EvDataExporter
{
    /// <summary>
    /// จัดการ MySQL connection และ export CSV
    /// ตาราง: db_thanes_conhis_system_nonthavej.tb_thaneshos_middle
    ///
    /// CSV spec (56 fields ตาม Data Dictionary):
    ///   Encode  : UTF-8 (no BOM)
    ///   Header  : ไม่มี
    ///   Quote   : ทุก field ครอบด้วย "…"
    ///   " ใน value → ""
    ///   Newline → *\n
    ///   CRLF ท้ายบรรทัด
    ///
    /// BinNum (field 42) : lookup จาก MSSQL ผ่าน MssqlLookup
    ///
    /// ชื่อไฟล์: YYYYMMDD_HHMMSS_SeqNo_PrescriptionNo.csv
    /// </summary>
    public class Databasetocsv : IDisposable
    {
        private MySqlConnection? _conn;

        public string ConnectionString { get; }
        public string SaveFolder { get; }
        public bool IsConnected { get; private set; }

        private int _seqNo = 0;
        private string _seqDate = "";

        private readonly PictureBox _picStatus;
        private readonly PictureBox _picDot;
        private readonly Label _lblStatus;

        // ── MSSQL Lookup สำหรับ BinNum (field 42) ────────────────────────
        private readonly MssqlLookup _mssqlLookup;

        private static readonly Color _green = Color.FromArgb(52, 199, 89);
        private static readonly Color _red = Color.FromArgb(255, 69, 58);

        private readonly string _sourceDb = "db_thanes_conhis_system_nonthavej";

        // ─────────────────────────────────────────────────────────────────
        public Databasetocsv(
            Config config,
            MssqlLookup mssqlLookup,
            PictureBox picStatus,
            PictureBox picDot,
            Label lblStatus)
        {
            if (!config.IsValid)
                throw new InvalidOperationException("Config ยังไม่ผ่าน validation");

            SaveFolder = config.SaveFolder;
            _mssqlLookup = mssqlLookup;
            _picStatus = picStatus;
            _picDot = picDot;
            _lblStatus = lblStatus;

            ConnectionString = new MySqlConnectionStringBuilder
            {
                Server = config.DbServer,
                Port = uint.TryParse(config.DbPort, out uint p) ? p : 3306,
                Database = config.DbName,
                UserID = config.DbUser,
                Password = config.DbPassword,
                ConnectionTimeout = 5,
                CharacterSet = "utf8mb4"
            }.ConnectionString;

            Logger.Info($"Databasetocsv created — SaveFolder={SaveFolder}");
        }

        // ─────────────────────────────────────────────────────────────────
        public async Task TestConnectionAsync()
        {
            SetText(_lblStatus, "Connecting…");
            Logger.Info("TestConnectionAsync — start");
            try
            {
                using var c = new MySqlConnection(ConnectionString);
                await c.OpenAsync();
                IsConnected = c.State == System.Data.ConnectionState.Open;
                Logger.Info("TestConnectionAsync — success");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Logger.Error("TestConnectionAsync — failed", ex);
            }

            PaintDot(_picStatus, IsConnected ? _green : _red);
            PaintDot(_picDot, IsConnected ? _green : _red);
            SetText(_lblStatus, IsConnected
                ? "Connected · Database OK"
                : "Disconnected · ตรวจสอบ config.ini");
        }

        // ─────────────────────────────────────────────────────────────────
        public async Task OpenAsync()
        {
            Logger.Info("OpenAsync — opening MySQL connection...");
            _conn = new MySqlConnection(ConnectionString);
            await _conn.OpenAsync();
            Logger.Info("OpenAsync — connection opened");
        }

        public MySqlConnection Conn =>
            _conn ?? throw new InvalidOperationException("ยังไม่ได้ OpenAsync()");

        // ─────────────────────────────────────────────────────────────────
        public async Task<int> ExportCsvAsync(
            IProgress<(int exported, int total)>? progress = null)
        {
            Logger.Info("ExportCsvAsync — querying pending records (MachineNo=11)...");

            int total;
            using (var cnt = Conn.CreateCommand())
            {
                cnt.CommandText =
                    $"SELECT COUNT(*) FROM {_sourceDb}.tb_thaneshos_middle " +
                    $"WHERE f_tomachineno = '11' AND f_dispensestatus_ev = 0";
                total = Convert.ToInt32(await cnt.ExecuteScalarAsync());
            }

            Logger.Info($"ExportCsvAsync — pending records: {total}");
            if (total == 0) return 0;

            // ── อ่าน records ทั้งหมดก่อน เพื่อ prefetch BinNum ทีเดียว ──
            var rows = new List<TbThaneshosMiddle>();
            using (var cmd = Conn.CreateCommand())
            {
                cmd.CommandText = BuildSelectQuery();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    rows.Add(MapRow(reader));
            }

            // ── Prefetch BinNum จาก MSSQL ทีเดียว (1 round-trip) ─────────
            var drugCds = rows.Select(r => r.DrugCd).Distinct().ToList();
            await _mssqlLookup.PrefetchAsync(drugCds);

            // ── Fill BinNum + derived fields ──────────────────────────────
            foreach (var row in rows)
            {
                row.BinNum = await _mssqlLookup.GetCassetteNoAsync(row.DrugCd);
                row.UpdateDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ApplyDerivedFields(row);
            }

            // ── เขียน CSV ─────────────────────────────────────────────────
            Directory.CreateDirectory(SaveFolder);
            Logger.Info($"ExportCsvAsync — SaveFolder: {SaveFolder}");

            var exportedIds = new List<string>();
            int count = 0;
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            foreach (var row in rows)
            {
                var now = DateTime.Now;
                var filePath = BuildFilePath(now, row.PrescriptionNo);

                Logger.Info($"Writing file: {Path.GetFileName(filePath)}");
                try
                {
                    await using var sw = new StreamWriter(filePath, append: false, utf8NoBom);
                    sw.NewLine = "\r\n";
                    await sw.WriteLineAsync(ToCsvRow(row));

                    exportedIds.Add(row.PrescriptionItemID);
                    count++;
                    progress?.Report((count, total));
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to write file: {filePath}", ex);
                }
            }

            // ── UPDATE exported flag ───────────────────────────────────────
            if (exportedIds.Count > 0)
            {
                Logger.Info($"Updating f_dispensestatus_ev=1 for {exportedIds.Count} record(s)...");
                const int batchSize = 500;
                for (int i = 0; i < exportedIds.Count; i += batchSize)
                {
                    var batch = exportedIds.Skip(i).Take(batchSize);
                    var inList = string.Join(",",
                        batch.Select(id => $"'{id.Replace("'", "''")}'"));
                    try
                    {
                        using var upd = Conn.CreateCommand();
                        upd.CommandText =
                            $"UPDATE {_sourceDb}.tb_thaneshos_middle " +
                            $"SET f_dispensestatus_ev = 1 " +
                            $"WHERE PrescriptionItemID IN ({inList})";
                        await upd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to update batch starting at index {i}", ex);
                    }
                }
                Logger.Info("Update complete");
            }

            Logger.Info($"ExportCsvAsync — finished. Exported: {count}/{total}");
            return count;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Derived field logic (ตาม Data Dictionary)
        // ─────────────────────────────────────────────────────────────────
        private static void ApplyDerivedFields(TbThaneshosMiddle r)
        {
            // ── Field 6: Sex  (M=1, F=2) ─────────────────────────────────
            r.Sex = r.Sex.Trim().ToUpper() switch
            {
                "M" => "1",
                "F" => "2",
                _ => r.Sex
            };

            // ── Field 7: PatCatCd  (O=1, I=2) ────────────────────────────
            r.PatCatCd = r.Raw_f_io_flag.Trim().ToUpper() switch
            {
                "O" => "1",
                "I" => "2",
                _ => r.Raw_f_io_flag
            };

            // ── Field 8: SpecCd = อายุ คำนวณจาก f_patientdob ─────────────
            r.SpecCd = CalcAge(r.Raw_f_patientdob);

            // ── Field 9: IOFlag  (O=1, I=2) ──────────────────────────────
            r.IOFlag = r.PatCatCd;   // ใช้ค่าเดียวกับ PatCatCd

            // ── Field 23: TradeName  ห่อด้วย "(…)" ───────────────────────
            if (!string.IsNullOrEmpty(r.TradeName))
                r.TradeName = $"({r.TradeName})";

            // ── Field 31: TicketNo  → "0001" คงที่ ───────────────────────
            r.TicketNo = "0001";

            // ── Field 32: CautionMsg  (f_priority 4,5,99 → "[HM]") ───────
            var priority = r.Raw_f_priority.Trim();
            r.CautionMsg = priority is "4" or "5" or "99" ? "[HM]" : "";

            // ── Fields 33-38: WarningMsg1-6  split newline f_aux_local_memo
            var warnParts = SplitNewline(r.Raw_f_aux_local_memo);
            r.WarningMsg1 = SafeIndex(warnParts, 0);
            r.WarningMsg2 = SafeIndex(warnParts, 1);
            r.WarningMsg3 = SafeIndex(warnParts, 2);
            r.WarningMsg4 = SafeIndex(warnParts, 3);
            r.WarningMsg5 = SafeIndex(warnParts, 4);
            r.WarningMsg6 = SafeIndex(warnParts, 5);

            // ── Field 40: PrescType  → "N" คงที่ ─────────────────────────
            r.PrescType = "N";

            // ── Field 41: FdnFlag  → "true" คงที่ ────────────────────────
            r.FdnFlag = "true";

            // ── Field 43: PrintLang  → "E" คงที่ ─────────────────────────
            r.PrintLang = "E";

            // ── Field 44: PrintType  → "N" คงที่ ─────────────────────────
            r.PrintType = "N";

            // ── Field 45: PrintIntsFlag  → "true" คงที่ ──────────────────
            r.PrintIntsFlag = "true";

            // ── Field 46: PrintBarcodeFlag  → "true" คงที่ ───────────────
            r.PrintBarcodeFlag = "true";

            // ── Field 47: PrintWardReturnFlag  → "false" คงที่ ───────────
            r.PrintWardReturnFlag = "false";

            // ── Fields 52-56: Reserve1-5  split newline f_noteprocessing ─
            var noteParts = SplitNewline(r.Raw_f_noteprocessing);
            r.Reserve1 = SafeIndex(noteParts, 0);
            r.Reserve2 = SafeIndex(noteParts, 1);
            r.Reserve3 = SafeIndex(noteParts, 2);
            r.Reserve4 = SafeIndex(noteParts, 3);
            r.Reserve5 = SafeIndex(noteParts, 4);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────

        /// <summary>คำนวณอายุ (ปี) จาก datestring → string  ถ้าไม่ได้ → ""</summary>
        private static string CalcAge(string dob)
        {
            if (string.IsNullOrWhiteSpace(dob)) return "";
            if (!DateTime.TryParse(dob, out var birth)) return "";
            var today = DateTime.Today;
            int age = today.Year - birth.Year;
            if (birth > today.AddYears(-age)) age--;
            return age >= 0 ? age.ToString() : "";
        }

        /// <summary>Split string ด้วย newline (CRLF / LF / CR)</summary>
        private static string[] SplitNewline(string? raw) =>
            string.IsNullOrEmpty(raw)
                ? Array.Empty<string>()
                : raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        /// <summary>ดึง index ปลอดภัย คืน "" ถ้าไม่มี</summary>
        private static string SafeIndex(string[] arr, int idx) =>
            idx < arr.Length ? arr[idx] : "";

        // ─────────────────────────────────────────────────────────────────
        //  CSV Row (56 fields ตาม Data Dictionary)
        // ─────────────────────────────────────────────────────────────────
        private static string ToCsvRow(TbThaneshosMiddle r) =>
            string.Join(",", new[]
            {
                /* 01 */ Q(r.PrescriptionNo),
                /* 02 */ Q(r.PatientID),
                /* 03 */ Q(r.PatientName),
                /* 04 */ Q(r.HkId),
                /* 05 */ Q(r.BirthDay),
                /* 06 */ Q(r.Sex),
                /* 07 */ Q(r.PatCatCd),
                /* 08 */ Q(r.SpecCd),
                /* 09 */ Q(r.IOFlag),
                /* 10 */ Q(r.HospitalCd),
                /* 11 */ Q(r.HospitalName),
                /* 12 */ Q(r.WorkStoreCd),
                /* 13 */ Q(r.WorkStationCd),
                /* 14 */ Q(r.WardCd),
                /* 15 */ Q(r.WardName),
                /* 16 */ Q(r.RoomNo),
                /* 17 */ Q(r.BedNo),
                /* 18 */ Q(r.DoctorCd),
                /* 19 */ Q(r.DoctorName),
                /* 20 */ Q(r.PrescriptionDate),
                /* 21 */ Q(r.DrugCd),
                /* 22 */ Q(r.DrugName),
                /* 23 */ Q(r.TradeName),
                /* 24 */ Q(r.DispensedDose),
                /* 25 */ Q(r.DispensedUnit),
                /* 26 */ Q(r.FormCd),
                /* 27 */ Q(r.FreqDescCd),
                /* 28 */ Q(r.FreqDesc1),
                /* 29 */ Q(r.FreqDesc2),
                /* 30 */ Q(r.ItemNo),
                /* 31 */ Q(r.TicketNo),
                /* 32 */ Q(r.CautionMsg),
                /* 33 */ QMsg(r.WarningMsg1),
                /* 34 */ QMsg(r.WarningMsg2),
                /* 35 */ QMsg(r.WarningMsg3),
                /* 36 */ QMsg(r.WarningMsg4),
                /* 37 */ QMsg(r.WarningMsg5),
                /* 38 */ QMsg(r.WarningMsg6),
                /* 39 */ Q(r.UserCd),
                /* 40 */ Q(r.PrescType),
                /* 41 */ Q(r.FdnFlag),
                /* 42 */ Q(r.BinNum),           // ← MSSQL lookup ln_CassetteNo
                /* 43 */ Q(r.PrintLang),
                /* 44 */ Q(r.PrintType),
                /* 45 */ Q(r.PrintIntsFlag),
                /* 46 */ Q(r.PrintBarcodeFlag),
                /* 47 */ Q(r.PrintWardReturnFlag),
                /* 48 */ Q(r.PreBarCd1),
                /* 49 */ Q(r.PreBarCd2),
                /* 50 */ Q(r.DeltaChangeInd),
                /* 51 */ Q(r.UpdateDate),
                /* 52 */ Q(r.Reserve1),
                /* 53 */ Q(r.Reserve2),
                /* 54 */ Q(r.Reserve3),
                /* 55 */ Q(r.Reserve4),
                /* 56 */ Q(r.Reserve5)
            });

        private static string Q(string? v) =>
            $"\"{(v ?? "").Replace("\"", "\"\"")}\"";

        private static string QMsg(string? v)
        {
            if (v is null) return "\"\"";
            var n = v.Replace("\r\n", "*\\n").Replace("\r", "*\\n").Replace("\n", "*\\n");
            return $"\"{n.Replace("\"", "\"\"")}\"";
        }

        // ─────────────────────────────────────────────────────────────────
        //  SELECT query
        // ─────────────────────────────────────────────────────────────────
        private string BuildSelectQuery() =>
            $@"SELECT
                PrescriptionItemID,
                f_prescriptionno        AS PrescriptionNo,
                f_hn                    AS PatientID,
                f_patientname           AS PatientName,
                f_an                    AS HkId,
                f_patientdob            AS BirthDay,
                f_patientdob            AS Raw_f_patientdob,
                f_sex                   AS Sex,
                f_io_flag               AS Raw_f_io_flag,
                f_pharmacylocationcode  AS WorkStoreCd,
                f_roomcode              AS RoomNo,
                f_doctorcode            AS DoctorCd,
                f_doctorname            AS DoctorName,
                f_prescriptiondate      AS PrescriptionDate,
                f_orderitemcode         AS DrugCd,
                f_orderitemname         AS DrugName,
                f_orderitemnameTH       AS TradeName,
                f_orderqty              AS DispensedDose,
                f_orderunitcode         AS DispensedUnit,
                f_frequencycode         AS FreqDescCd,
                f_frequencydesc         AS FreqDesc1,
                f_seq                   AS ItemNo,
                f_priority              AS Raw_f_priority,
                f_aux_local_memo        AS Raw_f_aux_local_memo,
                f_qr_code               AS PreBarCd1,
                f_noteprocessing        AS Raw_f_noteprocessing,
                f_dispensestatus_ev,
                f_tomachineno
            FROM {_sourceDb}.tb_thaneshos_middle
            WHERE f_tomachineno = '11'
              AND f_dispensestatus_ev = 0
            ORDER BY f_prescriptionno";

        // ─────────────────────────────────────────────────────────────────
        //  MapRow
        // ─────────────────────────────────────────────────────────────────
        private static TbThaneshosMiddle MapRow(System.Data.Common.DbDataReader r) => new()
        {
            PrescriptionItemID = Col(r, "PrescriptionItemID"),
            PrescriptionNo = Col(r, "PrescriptionNo"),
            PatientID = Col(r, "PatientID"),
            PatientName = Col(r, "PatientName"),
            HkId = Col(r, "HkId"),
            BirthDay = Col(r, "BirthDay"),
            Raw_f_patientdob = Col(r, "Raw_f_patientdob"),
            Sex = Col(r, "Sex"),
            Raw_f_io_flag = Col(r, "Raw_f_io_flag"),
            WorkStoreCd = Col(r, "WorkStoreCd"),
            WorkStationCd = Col(r, "WorkStoreCd"),   // ใช้ค่าเดิม field 12
            RoomNo = Col(r, "RoomNo"),
            BedNo = Col(r, "RoomNo"),         // ใช้ค่าเดิม field 16
            DoctorCd = Col(r, "DoctorCd"),
            DoctorName = Col(r, "DoctorName"),
            PrescriptionDate = Col(r, "PrescriptionDate"),
            DrugCd = Col(r, "DrugCd"),
            DrugName = Col(r, "DrugName"),
            TradeName = Col(r, "TradeName"),
            DispensedDose = Col(r, "DispensedDose"),
            DispensedUnit = Col(r, "DispensedUnit"),
            FreqDescCd = Col(r, "FreqDescCd"),
            FreqDesc1 = Col(r, "FreqDesc1"),
            ItemNo = Col(r, "ItemNo"),
            Raw_f_priority = Col(r, "Raw_f_priority"),
            Raw_f_aux_local_memo = Col(r, "Raw_f_aux_local_memo"),
            PreBarCd1 = Col(r, "PreBarCd1"),
            Raw_f_noteprocessing = Col(r, "Raw_f_noteprocessing"),
            f_tomachineno = Col(r, "f_tomachineno"),
        };

        private static string Col(System.Data.Common.DbDataReader r, string col)
        {
            int i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? "" : r.GetValue(i).ToString() ?? "";
        }

        // ─────────────────────────────────────────────────────────────────
        private string BuildFilePath(DateTime now, string prescriptionNo)
        {
            string today = now.ToString("yyyyMMdd");
            if (_seqDate != today) { _seqDate = today; _seqNo = 0; }
            _seqNo++;

            string safePresc = SanitizeFileName(prescriptionNo);
            string fileName = $"{today}_{now:HHmmss}_{_seqNo:D6}_{safePresc}.csv";
            return Path.Combine(SaveFolder, fileName);
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        // ── UI helpers ────────────────────────────────────────────────────
        private static void SetText(Control c, string v)
        {
            if (c.InvokeRequired) c.Invoke(() => c.Text = v);
            else c.Text = v;
        }

        private static void PaintDot(PictureBox pic, Color color)
        {
            if (pic.InvokeRequired) { pic.Invoke(() => PaintDot(pic, color)); return; }
            var bmp = new Bitmap(pic.Width, pic.Height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var br = new SolidBrush(color);
            g.FillEllipse(br, 0, 0, pic.Width - 1, pic.Height - 1);
            pic.Image = bmp;
        }

        public void Dispose()
        {
            Logger.Info("Databasetocsv disposed");
            _conn?.Dispose();
        }
    }
}