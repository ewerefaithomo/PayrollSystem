using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.Models
{
    public class DeActivate
    {
        public int Id { get; set; }

        [Required]
        public string FileNo { get; set; }

        [Required]
        public string? StaffID { get; set; }

        [Required]
        public string? FullName { get; set; }

        public string? Staff_Group { get; set; }

        public string? GradeLevel { get; set; }

        [Required]
        public string PaymentMonth { get; set; }

        [Required]
        public int PaymentYear { get; set; }

        //==============================
        // Earnings
        //==============================

        public decimal Consolidated { get; set; }

        public decimal TeachingAllowance { get; set; }

        public decimal CallDutyAllowance { get; set; }

        public decimal HazardAllowance { get; set; }

        public decimal ShiftAllowance { get; set; }

        public decimal SpecialistAllowance { get; set; }

        public decimal RuralPostingAllowance { get; set; }

        public decimal AccoutrementAllowance { get; set; }

        public decimal ConmessClinicalDutyAllowance { get; set; }

        public decimal ConmessNonClinicalAllowance { get; set; }

        public decimal PharmacistConsultAllowance { get; set; }

        public decimal Arrears { get; set; }

        public decimal Refund { get; set; }

        //==============================
        // Deductions
        //==============================

        public decimal OverPaymentDeduction { get; set; }

        public decimal Pension { get; set; }

        public decimal Tax { get; set; }

        //==============================
        // Totals
        //==============================

        public decimal Allowance { get; set; }

        public decimal Deductions { get; set; }

        public decimal Gross_Pay { get; set; }

        public decimal Net_Pay { get; set; }
        public decimal AMLSN { get; set; }
        public decimal ARD { get; set; }
        public decimal MEDCAN_DUES { get; set; }
        public decimal NARD { get; set; }
        public decimal NMA_DUES { get; set; }
        public decimal NMA_SPECIAL_LEVY { get; set; }
        public decimal RENT { get; set; }
        public decimal RENT_ARREARS { get; set; }
        public decimal TAX_ARREARS { get; set; }


        [Required]
        public string? Bank_Name { get; set; }

        [Required]
        public string? Bank_Branch { get; set; }

        [Required]
        public string? Account_Number { get; set; }

        public string Sort_Code { get; set; } = "";
    }
}