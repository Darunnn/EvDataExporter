namespace EvDataExporter
{
    /// <summary>
    /// Model สำหรับตาราง db_thanes_conhis_system_nonthavej.tb_thaneshos_middle
    /// + BinNum (field 42) ที่ lookup มาจาก MSSQL
    /// </summary>
    public class TbThaneshosMiddle
    {
        // ── Key fields ───────────────────────────────────────────────────
        public string PrescriptionItemID { get; set; } = "";   // ใช้ UPDATE
        public string PrescriptionNo { get; set; } = "";   // ชื่อไฟล์

        // ── Patient ──────────────────────────────────────────────────────
        public string PatientID { get; set; } = "";   // f_hn
        public string PatientName { get; set; } = "";   // f_patientname
        public string HkId { get; set; } = "";   // f_an
        public string BirthDay { get; set; } = "";   // f_patientdob (raw → คำนวณอายุสำหรับ SpecCd)
        public string Sex { get; set; } = "";   // f_sex  raw value

        // ── Classification ───────────────────────────────────────────────
        public string PatCatCd { get; set; } = "";   // f_io_flag  (O=1, I=2)
        public string SpecCd { get; set; } = "";   // คำนวณอายุจาก f_patientdob
        public string IOFlag { get; set; } = "";   // f_io_flag  (O=1, I=2)

        // ── Hospital / Ward ──────────────────────────────────────────────
        public string HospitalCd { get; set; } = "";
        public string HospitalName { get; set; } = "";
        public string WorkStoreCd { get; set; } = "";   // f_pharmacylocationcode
        public string WorkStationCd { get; set; } = "";   // f_pharmacylocationcode (ซ้ำกัน)
        public string WardCd { get; set; } = "";
        public string WardName { get; set; } = "";
        public string RoomNo { get; set; } = "";   // f_roomcode
        public string BedNo { get; set; } = "";   // f_roomcode (ซ้ำกัน)

        // ── Doctor ───────────────────────────────────────────────────────
        public string DoctorCd { get; set; } = "";   // f_doctorcode
        public string DoctorName { get; set; } = "";   // f_doctorname

        // ── Prescription ─────────────────────────────────────────────────
        public string PrescriptionDate { get; set; } = "";   // f_prescriptiondate

        // ── Drug ─────────────────────────────────────────────────────────
        public string DrugCd { get; set; } = "";   // f_orderitemcode
        public string DrugName { get; set; } = "";   // f_orderitemname
        public string TradeName { get; set; } = "";   // f_orderitemnameTH (จะห่อด้วย "(…)")
        public string DispensedDose { get; set; } = "";   // f_orderqty
        public string DispensedUnit { get; set; } = "";   // f_orderunitcode
        public string FormCd { get; set; } = "";

        // ── Frequency ────────────────────────────────────────────────────
        public string FreqDescCd { get; set; } = "";   // f_frequencycode
        public string FreqDesc1 { get; set; } = "";   // f_frequencydesc
        public string FreqDesc2 { get; set; } = "";

        // ── Item / Ticket ────────────────────────────────────────────────
        public string ItemNo { get; set; } = "";    // f_seq
        public string TicketNo { get; set; } = "0001"; // ค่าคงที่

        // ── Messages ─────────────────────────────────────────────────────
        // CautionMsg  : f_priority = 4,5,99 → "[HM]"  อื่นๆ → ""
        public string CautionMsg { get; set; } = "";
        // WarningMsg1-6 : split newline จาก f_aux_local_memo index 0-5
        public string WarningMsg1 { get; set; } = "";
        public string WarningMsg2 { get; set; } = "";
        public string WarningMsg3 { get; set; } = "";
        public string WarningMsg4 { get; set; } = "";
        public string WarningMsg5 { get; set; } = "";
        public string WarningMsg6 { get; set; } = "";

        // ── User / flags ─────────────────────────────────────────────────
        public string UserCd { get; set; } = "";
        public string PrescType { get; set; } = "N";       // ค่าคงที่
        public string FdnFlag { get; set; } = "true";    // ค่าคงที่

        // ── BinNum (field 42) ← MSSQL lookup ln_CassetteNo ──────────────
        public string BinNum { get; set; } = "";

        public string PrintLang { get; set; } = "E";       // ค่าคงที่
        public string PrintType { get; set; } = "N";       // ค่าคงที่
        public string PrintIntsFlag { get; set; } = "true";    // ค่าคงที่
        public string PrintBarcodeFlag { get; set; } = "true";    // ค่าคงที่
        public string PrintWardReturnFlag { get; set; } = "false";   // ค่าคงที่

        // ── Barcode ──────────────────────────────────────────────────────
        public string PreBarCd1 { get; set; } = "";   // f_qr_code
        public string PreBarCd2 { get; set; } = "";

        // ── Delta / Update ───────────────────────────────────────────────
        public string DeltaChangeInd { get; set; } = "";
        public string UpdateDate { get; set; } = "";   // datenow ตาม format ที่กำหนด

        // ── Reserves (split newline จาก f_noteprocessing index 0-4) ─────
        public string Reserve1 { get; set; } = "";
        public string Reserve2 { get; set; } = "";
        public string Reserve3 { get; set; } = "";
        public string Reserve4 { get; set; } = "";
        public string Reserve5 { get; set; } = "";

        // ── Internal (ไม่ export) ────────────────────────────────────────
        public string f_tomachineno { get; set; } = "";
        public int f_dispensestatus_ev { get; set; } = 0;

        // ── Raw fields (ใช้คำนวณ derived fields) ────────────────────────
        public string Raw_f_patientdob { get; set; } = "";   // สำหรับคำนวณ SpecCd (อายุ)
        public string Raw_f_io_flag { get; set; } = "";   // สำหรับ PatCatCd / IOFlag
        public string Raw_f_priority { get; set; } = "";   // สำหรับ CautionMsg
        public string Raw_f_aux_local_memo { get; set; } = ""; // สำหรับ WarningMsg1-6
        public string Raw_f_noteprocessing { get; set; } = ""; // สำหรับ Reserve1-5
    }
}