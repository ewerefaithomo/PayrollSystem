using System.Collections.Generic;

namespace PayrollSystem.Models
{
    public class MultipleDeactivateViewModel
    {
        // Staff IDs entered in the textarea
        public string StaffIds { get; set; }

        // Statistics
        public int TotalEntered { get; set; }

        public int TotalFound { get; set; }

        public int TotalDeactivated { get; set; }

        public int TotalNotFound { get; set; }

        // IDs not found in Payroll
        public List<string> NotFoundStaffIds { get; set; }
            = new List<string>();
    }
}