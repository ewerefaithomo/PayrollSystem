using System.Collections.Generic;

namespace PayrollSystem.Models
{
    public class MultipleActivateViewModel
    {
        public string StaffIds { get; set; }

        public int TotalEntered { get; set; }

        public int TotalFound { get; set; }

        public int TotalActivated { get; set; }

        public int TotalNotFound { get; set; }

        public List<string> NotFoundStaffIds { get; set; }

        public MultipleActivateViewModel()
        {
            NotFoundStaffIds = new List<string>();
        }
    }
}