using System.Collections.Generic;

namespace PayrollSystem.Models
{
    public class JournalReportViewModel
    {
        public string Month { get; set; }

        public int Year { get; set; }

        public List<JournalLine> JournalLines { get; set; }
            = new List<JournalLine>();

        public decimal TotalDebit { get; set; }

        public decimal TotalCredit { get; set; }
    }

    public class JournalLine
    {
        public int SN { get; set; }

        public string Description { get; set; }

        public int TotalStaff { get; set; }

        public decimal Debit { get; set; }

        public decimal Credit { get; set; }
    }
}