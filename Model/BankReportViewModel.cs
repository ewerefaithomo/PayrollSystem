using System.Collections.Generic;

namespace PayrollSystem.Models
{
    public class BankReportViewModel
    {
        public string Month { get; set; }

        public int Year { get; set; }

        public List<BankReportLine> BankLines { get; set; }
            = new List<BankReportLine>();

        public decimal TotalNetPay { get; set; }

        public int TotalStaff { get; set; }
    }

    public class BankReportLine
    {
        public int SN { get; set; }

        public string BankCode { get; set; }

        public string BankName { get; set; }

        public string Branch { get; set; }

        public decimal NetPay { get; set; }

        public int NoOfStaff { get; set; }
    }
}