using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.Models
{
    public class Payroll
    {
        public int Id { get; set; }

        //[Required]
        public string FileNo { get; set; } = "NIL";

        //[Required]
        public string? StaffID { get; set; } = "NIL";

        //[Required]
        public string? FullName { get; set; } = "NIL";

        public string? Staff_Group { get; set; } = "NIL";

        public string? GradeLevel { get; set; } = "NIL";

        //[Required]
        public string? PaymentMonth { get; set; } = "NIL";

        //[Required]
        public int PaymentYear { get; set; } = 0;

        // =========================================
        // EARNINGS
        // =========================================

        public decimal Consolidated { get; set; } = 0;

        public decimal TeachingAllowance { get; set; } = 0;

        public decimal CallDutyAllowance { get; set; } = 0;

        public decimal HazardAllowance { get; set; } = 0;

        public decimal ShiftAllowance { get; set; } = 0;

        public decimal SpecialistAllowance { get; set; } = 0;

        public decimal RuralPostingAllowance { get; set; } = 0;

        public decimal AccoutrementAllowance { get; set; } = 0;

        public decimal ConmessClinicalDutyAllowance { get; set; } = 0;

        public decimal ConmessNonClinicalAllowance { get; set; } = 0;

        public decimal PharmacistConsultAllowance { get; set; } = 0;

        public decimal Arrears { get; set; } = 0;

        public decimal Refund { get; set; } = 0;

        // =========================================
        // DEDUCTIONS
        // =========================================

        public decimal OverPaymentDeduction { get; set; } = 0;

        public decimal Pension { get; set; } = 0;

        public decimal Tax { get; set; } = 0;

        // =========================================
        // SYSTEM FIELDS
        // =========================================

        public decimal Allowance { get; set; } = 0;

        public decimal Deductions { get; set; } = 0;

        public decimal TAX_ARREARS { get; set; } = 0;
        public decimal AMLSN { get; set; } = 0;
        public decimal NMA_DUES { get; set; } = 0;
        public decimal NMA_SPECIAL_LEVY { get; set; } = 0;
        public decimal MEDCAN_DUES { get; set; } = 0;
        public decimal ARD { get; set; } = 0;
        public decimal NARD { get; set; } = 0;

        public decimal RENT { get; set; } = 0;
        public decimal RENT_ARREARS { get; set; } = 0;
        public decimal Gross_Pay { get; set; } = 0;

        public decimal Net_Pay { get; set; } = 0;

        //[Required]
        public string? Bank_Name { get; set; } = "NIL";

        //[Required]
        public string Bank_Branch { get; set; } = "NIL";

        //[Required]
        public string? Account_Number { get; set; } = "NIL";

        public string Sort_Code { get; set; } = "";
    }
}