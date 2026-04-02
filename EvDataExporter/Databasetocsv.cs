using MySql.Data.MySqlClient;
using System.Text;

namespace EvDataExporter
{
    /// <summary>
    /// จัดการ MySQL connection และ export CSV
    /// ตาราง: db_thanes_conhis_system_nonthavej.tb_thaneshos_middle
    ///
    /// CSV spec:
    ///   Encode  : UTF-8 (no BOM)
    ///   Header  : ไม่มี
    ///   Quote   : ทุก field ครอบด้วย "…"
    ///   " ใน value: double → ""
    ///   Newline ใน mass-print field (WarningMsg1-6, CautionMsg): แทนด้วย *\n
    ///   CRLF ท้ายบรรทัด
    ///
    /// ชื่อไฟล์: YYYYMMDD_HHMMSS_SeqNo_PrescriptionNo.csv
    /// </summary>
    public class Databasetocsv : IDisposable
    {
        // ── State ────────────────────────────────────────────────────────
        private MySqlConnection? _conn;

        public string ConnectionString { get; }
        public string SaveFolder { get; }
        public string MachineNo { get; }
        public bool IsConnected { get; private set; }

        // sequence counter รีเซ็ตทุกวัน
        private int _seqNo = 0;
        private string _seqDate = "";

        // ── UI refs ──────────────────────────────────────────────────────
        private readonly PictureBox _picStatus;
        private readonly PictureBox _picDot;
        private readonly Label _lblStatus;

        private static readonly Color _green = Color.FromArgb(52, 199, 89);
        private static readonly Color _red = Color.FromArgb(255, 69, 58);

        // Source DB name (อ่านจาก config)
        private readonly string _sourceDb;

        // ─────────────────────────────────────────────────────────────────
        public Databasetocsv(
            Config config,
            PictureBox picStatus,
            PictureBox picDot,
            Label lblStatus)
        {
            if (!config.IsValid)
                throw new InvalidOperationException("Config ยังไม่ผ่าน validation");

            SaveFolder = config.SaveFolder;
            MachineNo = config.MachineNo;
            _picStatus = picStatus;
            _picDot = picDot;
            _lblStatus = lblStatus;

            // ── ชื่อ source DB คงที่ตาม requirement ──────────────────────
            _sourceDb = "db_thanes_conhis_system_nonthavej";

            ConnectionString = new MySqlConnectionStringBuilder
            {
                Server = config.DbServer,
                Port = uint.TryParse(config.DbPort, out uint p) ? p : 3306,
                Database = config.DbName,   // db_thanes_system_pattaya
                UserID = config.DbUser,
                Password = config.DbPassword,
                ConnectionTimeout = 5,
                CharacterSet = "utf8mb4"
            }.ConnectionString;
        }

        // ─────────────────────────────────────────────────────────────────
        public async Task TestConnectionAsync()
        {
            SetText(_lblStatus, "Connecting…");
            try
            {
                using var c = new MySqlConnection(ConnectionString);
                await c.OpenAsync();
                IsConnected = c.State == System.Data.ConnectionState.Open;
            }
            catch { IsConnected = false; }

            PaintDot(_picStatus, IsConnected ? _green : _red);
            PaintDot(_picDot, IsConnected ? _green : _red);
            SetText(_lblStatus, IsConnected
                ? "Connected · Database OK"
                : "Disconnected · ตรวจสอบ config.ini");
        }

        // ─────────────────────────────────────────────────────────────────
        public async Task OpenAsync()
        {
            _conn = new MySqlConnection(ConnectionString);
            await _conn.OpenAsync();
        }

        public MySqlConnection Conn =>
            _conn ?? throw new InvalidOperationException("ยังไม่ได้ OpenAsync()");

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// ดึง tb_thaneshos_middle WHERE f_tomachineno=MachineNo AND f_dispensestatus_ev=0
        /// เขียน CSV แยกไฟล์ต่อ 1 record (no header, UTF-8 no BOM, CRLF)
        /// จากนั้น UPDATE f_dispensestatus_ev=1 ตาม PrescriptionItemID
        /// </summary>
        public async Task<int> ExportCsvAsync(
            IProgress<(int exported, int total)>? progress = null)
        {
            // ── นับ total ─────────────────────────────────────────────────
            int total;
            using (var cnt = Conn.CreateCommand())
            {
                cnt.CommandText =
                    $"SELECT COUNT(*) FROM {_sourceDb}.tb_thaneshos_middle " +
                    $"WHERE f_tomachineno = @mn AND f_dispensestatus_ev = 0";
                cnt.Parameters.AddWithValue("@mn", MachineNo);
                total = Convert.ToInt32(await cnt.ExecuteScalarAsync());
            }
            if (total == 0) return 0;

            // ── สร้างโฟลเดอร์ถ้ายังไม่มี ─────────────────────────────────
            Directory.CreateDirectory(SaveFolder);

            // ── Query ─────────────────────────────────────────────────────
            using var cmd = Conn.CreateCommand();
            cmd.CommandText = BuildSelectQuery();
            cmd.Parameters.AddWithValue("@mn", MachineNo);

            var exportedIds = new List<string>();
            int count = 0;

            // UTF-8 no BOM
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var row = MapRow(reader);
                    var now = DateTime.Now;

                    string filePath = BuildFilePath(now, row.PrescriptionNo);

                    // เขียน CSV: no header, CRLF ท้ายบรรทัด
                    await using var sw = new StreamWriter(
                        filePath, append: false, utf8NoBom);
                    sw.NewLine = "\r\n";    // force CRLF

                    await sw.WriteLineAsync(ToCsvRow(row));

                    exportedIds.Add(row.PrescriptionItemID);
                    count++;
                    progress?.Report((count, total));
                }
            }

            // ── UPDATE f_dispensestatus_ev = 1 ตาม PrescriptionItemID ────
            if (exportedIds.Count > 0)
            {
                // แบ่ง batch ป้องกัน query ยาวเกิน
                const int batchSize = 500;
                for (int i = 0; i < exportedIds.Count; i += batchSize)
                {
                    var batch = exportedIds.Skip(i).Take(batchSize);
                    var inList = string.Join(",",
                        batch.Select(id => $"'{id.Replace("'", "''")}'"));

                    using var upd = Conn.CreateCommand();
                    upd.CommandText =
                        $"UPDATE {_sourceDb}.tb_thaneshos_middle " +
                        $"SET f_dispensestatus_ev = 1 " +
                        $"WHERE PrescriptionItemID IN ({inList})";
                    await upd.ExecuteNonQueryAsync();
                }
            }

            return count;
        }

        // ─────────────────────────────────────────────────────────────────
        private string BuildFilePath(DateTime now, string prescriptionNo)
        {
            string today = now.ToString("yyyyMMdd");

            if (_seqDate != today)
            {
                _seqDate = today;
                _seqNo = 0;
            }
            _seqNo++;

            string safePresc = SanitizeFileName(prescriptionNo);
            // YYYYMMDD_HHMMSS_SeqNo_PrescriptionNo.csv
            string fileName = $"{today}_{now:HHmmss}_{_seqNo:D6}_{safePresc}.csv";
            return Path.Combine(SaveFolder, fileName);
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        // ─────────────────────────────────────────────────────────────────
        //  Query builder
        // ─────────────────────────────────────────────────────────────────
        private string BuildSelectQuery() =>
            $@"SELECT
                PrescriptionItemID,
                PrescriptionNo,
                PatientID,        PatientName,      HkId,
                BirthDay,         Sex,              PatCatCd,
                SpecCd,           IOFlag,           HospitalCd,
                HospitalName,     WorkStoreCd,      WardCd,
                WardName,         RoomNo,           BedNo,
                DoctorCd,         DoctorName,       PrescriptionDate,
                DrugCd,           DrugName,         TradeName,
                DispensedDose,    DispensedUnit,    FormCd,
                FreqDescCd,       FreqDesc1,        FreqDesc2,
                ItemNo,           TicketNo,         CautionMsg,
                WarningMsg1,      WarningMsg2,      WarningMsg3,
                WarningMsg4,      WarningMsg5,      WarningMsg6,
                UserCd,           PrescType,        FdnFlag,
                PrintLang,        PrintType,        PrintIntsFlag,
                PrintBarcodeFlag, PrintWardReturnFlag,
                PreBarCd1,        PreBarCd2,
                DeltaChangeInd,   UpdateDate,
                Reserve1,         Reserve2,         Reserve3,
                Reserve4,         Reserve5,
                f_tomachineno,    f_dispensestatus_ev
            FROM  {_sourceDb}.tb_thaneshos_middle
            WHERE f_tomachineno = @mn
              AND f_dispensestatus_ev = 0
            ORDER BY PrescriptionDate, PrescriptionNo";

        // ─────────────────────────────────────────────────────────────────
        //  CSV helpers
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// สร้าง CSV 1 บรรทัด (ไม่มี header)
        /// mass-print fields (CautionMsg, WarningMsg1-6): CRLF → *\n
        /// </summary>
        private static string ToCsvRow(TbThaneshosMiddle r) =>
            string.Join(",", new[]
            {
                Q(r.PrescriptionNo),        Q(r.PatientID),           Q(r.PatientName),
                Q(r.HkId),                  Q(r.BirthDay),            Q(r.Sex),
                Q(r.PatCatCd),              Q(r.SpecCd),              Q(r.IOFlag),
                Q(r.HospitalCd),            Q(r.HospitalName),        Q(r.WorkStoreCd),
                Q(r.WardCd),               Q(r.WardName),            Q(r.RoomNo),
                Q(r.BedNo),                 Q(r.DoctorCd),            Q(r.DoctorName),
                Q(r.PrescriptionDate),      Q(r.DrugCd),              Q(r.DrugName),
                Q(r.TradeName),             Q(r.DispensedDose),       Q(r.DispensedUnit),
                Q(r.FormCd),                Q(r.FreqDescCd),          Q(r.FreqDesc1),
                Q(r.FreqDesc2),             Q(r.ItemNo),              Q(r.TicketNo),
                QMsg(r.CautionMsg),
                QMsg(r.WarningMsg1),        QMsg(r.WarningMsg2),      QMsg(r.WarningMsg3),
                QMsg(r.WarningMsg4),        QMsg(r.WarningMsg5),      QMsg(r.WarningMsg6),
                Q(r.UserCd),                Q(r.PrescType),           Q(r.FdnFlag),
                Q(r.PrintLang),             Q(r.PrintType),           Q(r.PrintIntsFlag),
                Q(r.PrintBarcodeFlag),      Q(r.PrintWardReturnFlag),
                Q(r.PreBarCd1),             Q(r.PreBarCd2),
                Q(r.DeltaChangeInd),        Q(r.UpdateDate),
                Q(r.Reserve1),              Q(r.Reserve2),            Q(r.Reserve3),
                Q(r.Reserve4),              Q(r.Reserve5)
            });

        /// <summary>Quote ธรรมดา: " → "" ไม่แตะ newline</summary>
        private static string Q(string? v) =>
            $"\"{(v ?? "").Replace("\"", "\"\"")}\"";

        /// <summary>
        /// Quote สำหรับ mass-print fields:
        /// CRLF / LF / CR → *\n  จากนั้น " → ""
        /// </summary>
        private static string QMsg(string? v)
        {
            if (v is null) return "\"\"";
            // แทน newline ทุกรูปแบบด้วย *\n
            var normalized = v
                .Replace("\r\n", "*\\n")
                .Replace("\r", "*\\n")
                .Replace("\n", "*\\n");
            return $"\"{normalized.Replace("\"", "\"\"")}\"";
        }

        // ─────────────────────────────────────────────────────────────────
        //  Row mapper
        // ─────────────────────────────────────────────────────────────────
        private static TbThaneshosMiddle MapRow(System.Data.Common.DbDataReader r) => new()
        {
            PrescriptionItemID = Col(r, "PrescriptionItemID"),
            PrescriptionNo = Col(r, "PrescriptionNo"),
            PatientID = Col(r, "PatientID"),
            PatientName = Col(r, "PatientName"),
            HkId = Col(r, "HkId"),
            BirthDay = Col(r, "BirthDay"),
            Sex = Col(r, "Sex"),
            PatCatCd = Col(r, "PatCatCd"),
            SpecCd = Col(r, "SpecCd"),
            IOFlag = Col(r, "IOFlag"),
            HospitalCd = Col(r, "HospitalCd"),
            HospitalName = Col(r, "HospitalName"),
            WorkStoreCd = Col(r, "WorkStoreCd"),
            WardCd = Col(r, "WardCd"),
            WardName = Col(r, "WardName"),
            RoomNo = Col(r, "RoomNo"),
            BedNo = Col(r, "BedNo"),
            DoctorCd = Col(r, "DoctorCd"),
            DoctorName = Col(r, "DoctorName"),
            PrescriptionDate = Col(r, "PrescriptionDate"),
            DrugCd = Col(r, "DrugCd"),
            DrugName = Col(r, "DrugName"),
            TradeName = Col(r, "TradeName"),
            DispensedDose = Col(r, "DispensedDose"),
            DispensedUnit = Col(r, "DispensedUnit"),
            FormCd = Col(r, "FormCd"),
            FreqDescCd = Col(r, "FreqDescCd"),
            FreqDesc1 = Col(r, "FreqDesc1"),
            FreqDesc2 = Col(r, "FreqDesc2"),
            ItemNo = Col(r, "ItemNo"),
            TicketNo = Col(r, "TicketNo"),
            CautionMsg = Col(r, "CautionMsg"),
            WarningMsg1 = Col(r, "WarningMsg1"),
            WarningMsg2 = Col(r, "WarningMsg2"),
            WarningMsg3 = Col(r, "WarningMsg3"),
            WarningMsg4 = Col(r, "WarningMsg4"),
            WarningMsg5 = Col(r, "WarningMsg5"),
            WarningMsg6 = Col(r, "WarningMsg6"),
            UserCd = Col(r, "UserCd"),
            PrescType = Col(r, "PrescType"),
            FdnFlag = Col(r, "FdnFlag"),
            PrintLang = Col(r, "PrintLang"),
            PrintType = Col(r, "PrintType"),
            PrintIntsFlag = Col(r, "PrintIntsFlag"),
            PrintBarcodeFlag = Col(r, "PrintBarcodeFlag"),
            PrintWardReturnFlag = Col(r, "PrintWardReturnFlag"),
            PreBarCd1 = Col(r, "PreBarCd1"),
            PreBarCd2 = Col(r, "PreBarCd2"),
            DeltaChangeInd = Col(r, "DeltaChangeInd"),
            UpdateDate = Col(r, "UpdateDate"),
            Reserve1 = Col(r, "Reserve1"),
            Reserve2 = Col(r, "Reserve2"),
            Reserve3 = Col(r, "Reserve3"),
            Reserve4 = Col(r, "Reserve4"),
            Reserve5 = Col(r, "Reserve5"),
            f_tomachineno = Col(r, "f_tomachineno"),
        };

        private static string Col(System.Data.Common.DbDataReader r, string col)
        {
            int i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? "" : r.GetValue(i).ToString() ?? "";
        }

        // ─────────────────────────────────────────────────────────────────
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

        public void Dispose() => _conn?.Dispose();
    }
}