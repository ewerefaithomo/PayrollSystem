using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PayrollSystem.Models
{
    public class PaySlipViewModel
    {
        public string StaffID { get; set; }

        public string SelectedMonth { get; set; }

        public int SelectedYear { get; set; }

        public Payroll PayrollRecord { get; set; }

        public List<SelectListItem> Months { get; set; }

        public List<SelectListItem> Years { get; set; }
    }
}