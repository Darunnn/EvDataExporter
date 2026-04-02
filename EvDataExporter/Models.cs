namespace EvDataExporter
{
    /// <summary>
    /// Model สำหรับตาราง db_thanes_conhis_system_nonthavej.tb_thaneshos_middle
    /// </summary>
    public class TbThaneshosMiddle
    {
        // ── Key fields ───────────────────────────────────────────────────
        public string PrescriptionItemID { get; set; } = "";   // ใช้ UPDATE
        public string PrescriptionNo { get; set; } = "";   // ชื่อไฟล์

        // ── Patient ──────────────────────────────────────────────────────
        public string PatientID { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string HkId { get; set; } = "";
        public string BirthDay { get; set; } = "";
        public string Sex { get; set; } = "";

        // ── Classification ───────────────────────────────────────────────
        public string PatCatCd { get; set; } = "";
        public string SpecCd { get; set; } = "";
        public string IOFlag { get; set; } = "";

        // ── Hospital / Ward ──────────────────────────────────────────────
        public string HospitalCd { get; set; } = "";
        public string HospitalName { get; set; } = "";
        public string WorkStoreCd { get; set; } = "";
        public string WardCd { get; set; } = "";
        public string WardName { get; set; } = "";
        public string RoomNo { get; set; } = "";
        public string BedNo { get; set; } = "";

        // ── Doctor ───────────────────────────────────────────────────────
        public string DoctorCd { get; set; } = "";
        public string DoctorName { get; set; } = "";

        // ── Prescription ─────────────────────────────────────────────────
        public string PrescriptionDate { get; set; } = "";

        // ── Drug ─────────────────────────────────────────────────────────
        public string DrugCd { get; set; } = "";
        public string DrugName { get; set; } = "";
        public string TradeName { get; set; } = "";
        public string DispensedDose { get; set; } = "";
        public string DispensedUnit { get; set; } = "";
        public string FormCd { get; set; } = "";

        // ── Frequency ────────────────────────────────────────────────────
        public string FreqDescCd { get; set; } = "";
        public string FreqDesc1 { get; set; } = "";
        public string FreqDesc2 { get; set; } = "";

        // ── Item / Ticket ────────────────────────────────────────────────
        public string ItemNo { get; set; } = "";
        public string TicketNo { get; set; } = "";

        // ── Messages (mass print 1-6: CRLF → *\n) ───────────────────────
        public string CautionMsg { get; set; } = "";
        public string WarningMsg1 { get; set; } = "";
        public string WarningMsg2 { get; set; } = "";
        public string WarningMsg3 { get; set; } = "";
        public string WarningMsg4 { get; set; } = "";
        public string WarningMsg5 { get; set; } = "";
        public string WarningMsg6 { get; set; } = "";

        // ── User / Print flags ───────────────────────────────────────────
        public string UserCd { get; set; } = "";
        public string PrescType { get; set; } = "";
        public string FdnFlag { get; set; } = "";
        public string PrintLang { get; set; } = "";
        public string PrintType { get; set; } = "";
        public string PrintIntsFlag { get; set; } = "";
        public string PrintBarcodeFlag { get; set; } = "";
        public string PrintWardReturnFlag { get; set; } = "";

        // ── Barcode ──────────────────────────────────────────────────────
        public string PreBarCd1 { get; set; } = "";
        public string PreBarCd2 { get; set; } = "";

        // ── Delta / Update ───────────────────────────────────────────────
        public string DeltaChangeInd { get; set; } = "";
        public string UpdateDate { get; set; } = "";

        // ── Reserves ─────────────────────────────────────────────────────
        public string Reserve1 { get; set; } = "";
        public string Reserve2 { get; set; } = "";
        public string Reserve3 { get; set; } = "";
        public string Reserve4 { get; set; } = "";
        public string Reserve5 { get; set; } = "";

        // ── Machine / Status (internal, ไม่ export) ──────────────────────
        public string f_tomachineno { get; set; } = "";
        public int f_dispensestatus_ev { get; set; } = 0;
    }
}