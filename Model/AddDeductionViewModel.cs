using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.Models
{
    public class AddDeductionViewModel
    {
        [Required(ErrorMessage = "Please select a deduction head.")]
        public string DeductionHead { get; set; }

        [Required(ErrorMessage = "Please enter Staff IDs and deduction amounts.")]
        public string DeductionData { get; set; }
    }
}