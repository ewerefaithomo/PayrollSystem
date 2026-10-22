using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PayrollSystem.Controllers
{
    public class PayslipController : Controller
    {
        private readonly AppDbContext _context;

        public PayslipController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var viewModel = new PaySlipViewModel
            {
                Months = GetMonthsList(),
                Years = GetYearsList()
            };
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> SearchPayslip(PaySlipViewModel model)
        {
            // Validate input parameters
            if (string.IsNullOrWhiteSpace(model.StaffID) ||
                string.IsNullOrWhiteSpace(model.SelectedMonth) ||
                model.SelectedYear == 0)
            {
                ViewBag.Message = "Please provide Staff ID, Month, and Year.";
                model.Months = GetMonthsList();
                model.Years = GetYearsList();
                return View("Index", model);
            }

            Payroll payrollRecord = null;

            // 1. Search the primary Payroll table first (comparing integer year directly)
            var primaryRecord = await _context.Payroll
                .FirstOrDefaultAsync(p => p.StaffID == model.StaffID
                                       && p.PaymentMonth == model.SelectedMonth
                                       && p.PaymentYear == model.SelectedYear);

            if (primaryRecord != null)
            {
                payrollRecord = primaryRecord;
            }
            else
            {
                // 2. If not found in primary Payroll, search the BackupPayroll table
                var backupRecord = await _context.BackupPayroll
                    .FirstOrDefaultAsync(b => b.StaffID == model.StaffID
                                           && b.PaymentMonth == model.SelectedMonth
                                           && b.PaymentYear == model.SelectedYear);

                if (backupRecord != null)
                {
                    // Map BackupPayroll properties to the Payroll model structure for view rendering
                    payrollRecord = new Payroll
                    {
                        Id = backupRecord.Id,
                        StaffID = backupRecord.StaffID,
                        FullName = backupRecord.FullName,
                        GradeLevel = backupRecord.GradeLevel,
                        Staff_Group = backupRecord.Staff_Group,
                        Bank_Name = backupRecord.Bank_Name,
                        Bank_Branch = backupRecord.Bank_Branch,
                        Account_Number = backupRecord.Account_Number,
                        PaymentMonth = backupRecord.PaymentMonth,
                        PaymentYear = backupRecord.PaymentYear,
                        Consolidated = backupRecord.Consolidated,
                        Allowance = backupRecord.Allowance,
                        TeachingAllowance = backupRecord.TeachingAllowance,
                        CallDutyAllowance = backupRecord.CallDutyAllowance,
                        HazardAllowance = backupRecord.HazardAllowance,
                        ShiftAllowance = backupRecord.ShiftAllowance,
                        SpecialistAllowance = backupRecord.SpecialistAllowance,
                        RuralPostingAllowance = backupRecord.RuralPostingAllowance,
                        AccoutrementAllowance = backupRecord.AccoutrementAllowance,
                        ConmessClinicalDutyAllowance = backupRecord.ConmessClinicalDutyAllowance,
                        ConmessNonClinicalAllowance = backupRecord.ConmessNonClinicalAllowance,
                        PharmacistConsultAllowance = backupRecord.PharmacistConsultAllowance,
                        Arrears = backupRecord.Arrears,
                        Refund = backupRecord.Refund,
                        OverPaymentDeduction = backupRecord.OverPaymentDeduction,
                        Pension = backupRecord.Pension,
                        Tax = backupRecord.Tax,
                        TAX_ARREARS = backupRecord.TAX_ARREARS,
                        AMLSN = backupRecord.AMLSN,
                        NMA_DUES = backupRecord.NMA_DUES,
                        NMA_SPECIAL_LEVY = backupRecord.NMA_SPECIAL_LEVY,
                        MEDCAN_DUES = backupRecord.MEDCAN_DUES,
                        ARD = backupRecord.ARD,
                        NARD = backupRecord.NARD,
                        RENT = backupRecord.RENT,
                        RENT_ARREARS = backupRecord.RENT_ARREARS,
                        Gross_Pay = backupRecord.Gross_Pay,
                        Deductions = backupRecord.Deductions,
                        Net_Pay = backupRecord.Net_Pay
                    };
                }
            }

            // 3. If still not found in either table, display the "No Record Found" message
            if (payrollRecord == null)
            {
                ViewBag.Message = "No Record Found";
                model.Months = GetMonthsList();
                model.Years = GetYearsList();
                return View("Index", model);
            }

            // Record found in one of the tables, pass it to the view
            model.PayrollRecord = payrollRecord;
            return View("Payslip", model);
        }

        private List<SelectListItem> GetMonthsList()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Text = "January", Value = "January" },
                new SelectListItem { Text = "February", Value = "February" },
                new SelectListItem { Text = "March", Value = "March" },
                new SelectListItem { Text = "April", Value = "April" },
                new SelectListItem { Text = "May", Value = "May" },
                new SelectListItem { Text = "June", Value = "June" },
                new SelectListItem { Text = "July", Value = "July" },
                new SelectListItem { Text = "August", Value = "August" },
                new SelectListItem { Text = "September", Value = "September" },
                new SelectListItem { Text = "October", Value = "October" },
                new SelectListItem { Text = "November", Value = "November" },
                new SelectListItem { Text = "December", Value = "December" }
            };
        }

        private List<SelectListItem> GetYearsList()
        {
            int currentYear = DateTime.Now.Year;
            var years = new List<SelectListItem>();
            for (int y = currentYear; y >= currentYear - 5; y--)
            {
                years.Add(new SelectListItem { Text = y.ToString(), Value = y.ToString() });
            }
            return years;
        }
    }
}