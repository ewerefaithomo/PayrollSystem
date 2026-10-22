using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OfficeOpenXml;
using PayrollSystem.Data;
using PayrollSystem.Models;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PayrollSystem.Controllers
{
    public class PayrollController : Controller
    {
        private readonly AppDbContext _context;

        public PayrollController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // PROCESS PAGE
        // =========================================================

        public IActionResult Process()
        {
            return View();
        }

        // =========================================================
        // ADD DEDUCTION PAGE
        // =========================================================

        [HttpGet]
        public IActionResult AddDeduction()
        {
            return View(new AddDeductionViewModel());
        }

        // =========================================================
        // PROCESS ADD DEDUCTION
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessAddDeduction(AddDeductionViewModel model)
        {
            if (model == null)
            {
                return View("AddDeduction", new AddDeductionViewModel());
            }

            if (string.IsNullOrWhiteSpace(model.DeductionHead))
            {
                ModelState.AddModelError("DeductionHead", "Please select a deduction head.");
                return View("AddDeduction", model);
            }

            if (!IsValidDeductionHead(model.DeductionHead))
            {
                ModelState.AddModelError("DeductionHead", $"Invalid deduction head: {model.DeductionHead}");
                return View("AddDeduction", model);
            }

            if (string.IsNullOrWhiteSpace(model.DeductionData))
            {
                ModelState.AddModelError("DeductionData", "Please enter Staff IDs and deduction amounts.");
                return View("AddDeduction", model);
            }

            var lines = model.DeductionData
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (lines.Count == 0)
            {
                ModelState.AddModelError("DeductionData", "No valid Staff ID and deduction amount was entered.");
                return View("AddDeduction", model);
            }

            int totalSubmitted = lines.Count;
            int totalAdded = 0;

            var failedStaffIds = new List<string>();

            using IDbContextTransaction transaction = _context.Database.BeginTransaction();

            try
            {
                foreach (string line in lines)
                {
                    string[] parts = line.Split(',');

                    if (parts.Length != 2)
                    {
                        failedStaffIds.Add(line);
                        continue;
                    }

                    string staffId = parts[0].Trim();
                    string amountText = parts[1].Trim();

                    if (string.IsNullOrWhiteSpace(staffId))
                    {
                        failedStaffIds.Add(line);
                        continue;
                    }

                    if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
                    {
                        failedStaffIds.Add(staffId);
                        continue;
                    }

                    if (amount <= 0)
                    {
                        failedStaffIds.Add(staffId);
                        continue;
                    }

                    var payroll = _context.Payroll.FirstOrDefault(x => x.StaffID == staffId);

                    if (payroll == null)
                    {
                        failedStaffIds.Add(staffId);
                        continue;
                    }

                    // Add the deduction to the selected field.
                    UpdateDeductionField(payroll, model.DeductionHead, amount);

                    // Always recalculate from individual fields.
                    RecalculatePayroll(payroll);

                    totalAdded++;
                }

                _context.SaveChanges();
                transaction.Commit();

                ViewBag.SuccessMessage = "Deduction processing completed successfully.";
                ViewBag.TotalSubmitted = totalSubmitted;
                ViewBag.TotalAdded = totalAdded;
                ViewBag.TotalFailed = failedStaffIds.Count;

                if (failedStaffIds.Count > 0)
                {
                    ViewBag.FailedList = string.Join(Environment.NewLine, failedStaffIds);
                }

                ModelState.Clear();

                return View("AddDeduction", new AddDeductionViewModel());
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ModelState.AddModelError("", "An error occurred while processing the deductions.");
                ModelState.AddModelError("", ex.Message);

                return View("AddDeduction", model);
            }
        }

        // =========================================================
        // PROCESS PAYROLL
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessPayroll()
        {
            try
            {
                var payrollRecords = _context.Payroll.ToList();

                if (payrollRecords == null || payrollRecords.Count == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "There are no payroll records available for processing.",
                        totalRecords = 0
                    });
                }

                foreach (var item in payrollRecords)
                {
                    item.Sort_Code = GetBankSortCode(item.Bank_Name);

                    // Pad Account Number with leading zeros if it is less than 10 digits
                    if (!string.IsNullOrWhiteSpace(item.Account_Number) && item.Account_Number != "NIL" && item.Account_Number.Length < 10)
                    {
                        item.Account_Number = item.Account_Number.Trim().PadLeft(10, '0');
                    }

                    RecalculatePayroll(item);
                }

                _context.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Payroll processing completed successfully.",
                    totalRecords = payrollRecords.Count
                });
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += " | " + ex.InnerException.Message;
                }

                Console.WriteLine("PAYROLL PROCESSING ERROR: " + errorMessage);

                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while processing payroll.",
                    error = errorMessage
                });
            }
        }

        // =========================================================
        // UPDATE STAFF PAY RECORD PAGE
        // =========================================================

        [HttpGet]
        public IActionResult Update()
        {
            return View();
        }

        // =========================================================
        // SEARCH STAFF RECORD
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SearchStaff(string staffId)
        {
            if (string.IsNullOrWhiteSpace(staffId))
            {
                ViewBag.ErrorMessage = "Please enter Staff ID";
                return View("Update");
            }

            var payroll = _context.Payroll
                .FirstOrDefault(x => x.StaffID == staffId.Trim());

            if (payroll == null)
            {
                ViewBag.ErrorMessage = "No record found";
                return View("Update");
            }

            return View("EditStaffRecord", payroll);
        }

        // =========================================================
        // UPDATE STAFF RECORD (Configured to throw errors)
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateStaffRecord(Payroll payroll)
        {
            if (payroll == null)
            {
                throw new ArgumentNullException(nameof(payroll), "Submitted payroll model bound to null.");
            }

            var existingRecord = _context.Payroll
                .FirstOrDefault(x => x.Id == payroll.Id || (!string.IsNullOrEmpty(x.StaffID) && x.StaffID == payroll.StaffID));

            if (existingRecord == null)
            {
                throw new KeyNotFoundException($"Database lookup failed: No payroll record found matching ID '{payroll.Id}' or Staff ID '{payroll.StaffID}'.");
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                throw new InvalidOperationException($"ModelState is invalid. Validation Errors: {errors}");
            }

            existingRecord.FileNo = payroll.FileNo;
            existingRecord.FullName = payroll.FullName;
            existingRecord.Staff_Group = payroll.Staff_Group;
            existingRecord.GradeLevel = payroll.GradeLevel;
            existingRecord.PaymentMonth = payroll.PaymentMonth;
            existingRecord.PaymentYear = payroll.PaymentYear;

            existingRecord.Bank_Name = payroll.Bank_Name;
            existingRecord.Bank_Branch = payroll.Bank_Branch;
            existingRecord.Account_Number = payroll.Account_Number;

            existingRecord.Sort_Code = GetBankSortCode(payroll.Bank_Name);

            existingRecord.Consolidated = payroll.Consolidated;
            existingRecord.Allowance = payroll.Allowance;

            existingRecord.TeachingAllowance = payroll.TeachingAllowance;
            existingRecord.CallDutyAllowance = payroll.CallDutyAllowance;
            existingRecord.HazardAllowance = payroll.HazardAllowance;
            existingRecord.ShiftAllowance = payroll.ShiftAllowance;
            existingRecord.SpecialistAllowance = payroll.SpecialistAllowance;
            existingRecord.RuralPostingAllowance = payroll.RuralPostingAllowance;
            existingRecord.AccoutrementAllowance = payroll.AccoutrementAllowance;
            existingRecord.ConmessClinicalDutyAllowance = payroll.ConmessClinicalDutyAllowance;
            existingRecord.ConmessNonClinicalAllowance = payroll.ConmessNonClinicalAllowance;
            existingRecord.PharmacistConsultAllowance = payroll.PharmacistConsultAllowance;
            existingRecord.Arrears = payroll.Arrears;

            existingRecord.Refund = payroll.Refund;
            existingRecord.OverPaymentDeduction = payroll.OverPaymentDeduction;
            existingRecord.Pension = payroll.Pension;
            existingRecord.Tax = payroll.Tax;
            existingRecord.TAX_ARREARS = payroll.TAX_ARREARS;
            existingRecord.AMLSN = payroll.AMLSN;
            existingRecord.NMA_DUES = payroll.NMA_DUES;
            existingRecord.NMA_SPECIAL_LEVY = payroll.NMA_SPECIAL_LEVY;
            existingRecord.MEDCAN_DUES = payroll.MEDCAN_DUES;
            existingRecord.ARD = payroll.ARD;
            existingRecord.NARD = payroll.NARD;
            existingRecord.RENT = payroll.RENT;
            existingRecord.RENT_ARREARS = payroll.RENT_ARREARS;

            RecalculatePayroll(existingRecord);

            _context.Payroll.Update(existingRecord);
            _context.SaveChanges();

            ViewBag.SuccessMessage = $"Record for Staff ID ({existingRecord.StaffID}) updated successfully.";

            return View("EditStaffRecord", existingRecord);
        }

        // =========================================================
        // PAYSLIP PAGE
        // =========================================================

        [HttpGet]
        public IActionResult Payslip()
        {
            var model = new PaySlipViewModel
            {
                Months = GetMonths(),
                Years = GetYears()
            };

            return View(model);
        }

        // =========================================================
        // SEARCH SINGLE PAYSLIP (Updated to check Payroll & BackupPayroll)
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SearchPayslip(PaySlipViewModel model)
        {
            model.Months = GetMonths();
            model.Years = GetYears();

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(model.StaffID) ||
                string.IsNullOrWhiteSpace(model.SelectedMonth) ||
                model.SelectedYear == 0)
            {
                ViewBag.Message = "Please provide Staff ID, Month, and Year.";
                return View("Payslip", model);
            }

            Payroll payrollRecord = null;

            // 1. Search the primary Payroll table first
            var primaryRecord = await _context.Payroll
                .FirstOrDefaultAsync(x => x.StaffID == model.StaffID.Trim() &&
                                          x.PaymentMonth.ToLower() == model.SelectedMonth.ToLower() &&
                                          x.PaymentYear == model.SelectedYear);

            if (primaryRecord != null)
            {
                payrollRecord = primaryRecord;
            }
            else
            {
                // 2. If not found in primary Payroll, search the BackupPayroll table
                var backupRecord = await _context.BackupPayroll
                    .FirstOrDefaultAsync(b => b.StaffID == model.StaffID.Trim() &&
                                              b.PaymentMonth.ToLower() == model.SelectedMonth.ToLower() &&
                                              b.PaymentYear == model.SelectedYear);

                if (backupRecord != null)
                {
                    // Map BackupPayroll properties to the Payroll model structure for view rendering
                    payrollRecord = new Payroll
                    {
                        Id = backupRecord.Id,
                        FileNo = backupRecord.FileNo,
                        StaffID = backupRecord.StaffID,
                        FullName = backupRecord.FullName,
                        Staff_Group = backupRecord.Staff_Group,
                        GradeLevel = backupRecord.GradeLevel,
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
                        Deductions = backupRecord.Deductions,
                        Gross_Pay = backupRecord.Gross_Pay,
                        Net_Pay = backupRecord.Net_Pay,
                        Bank_Name = backupRecord.Bank_Name,
                        Bank_Branch = backupRecord.Bank_Branch,
                        Account_Number = backupRecord.Account_Number,
                        Sort_Code = backupRecord.Sort_Code
                    };
                }
            }

            // 3. If still not found in either table, display "No record found"
            if (payrollRecord == null)
            {
                ViewBag.Message = "No record found";
                return View("Payslip", model);
            }

            model.PayrollRecord = payrollRecord;
            return View("PayslipResult", model);
        }

        // =========================================================
        // EXPORT SINGLE PAYSLIP PDF
        // =========================================================

        public IActionResult ExportPdf(string staffId, string month, int year)
        {
            var payrollRecord = _context.Payroll
                .FirstOrDefault(x =>
                    x.StaffID == staffId &&
                    x.PaymentMonth == month &&
                    x.PaymentYear == year);

            if (payrollRecord == null)
            {
                return NotFound();
            }

            var model = new PaySlipViewModel
            {
                PayrollRecord = payrollRecord
            };

            return new ViewAsPdf("PayslipPdf", model)
            {
                FileName = $"Payslip_{staffId}_{month}_{year}.pdf"
            };
        }

        // =========================================================
        // SEARCH GROUP PAYSLIPS (Updated to check Payroll & BackupPayroll)
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SearchGroupPayslip(string staffGroup, string month, int year)
        {
            if (string.IsNullOrWhiteSpace(staffGroup) || string.IsNullOrWhiteSpace(month) || year <= 0)
            {
                var model = new PaySlipViewModel
                {
                    Months = GetMonths(),
                    Years = GetYears()
                };
                ViewBag.GroupMessage = "Please provide Staff Group, Month, and Year.";
                return View("Payslip", model);
            }

            var payrolls = _context.Payroll
                .Where(x =>
                    x.Staff_Group == staffGroup &&
                    x.PaymentMonth.ToLower() == month.ToLower() &&
                    x.PaymentYear == year)
                .OrderBy(x => x.FullName)
                .ToList();

            // If not found in primary Payroll, search the BackupPayroll table
            if (payrolls.Count == 0)
            {
                var backupPayrolls = _context.BackupPayroll
                    .Where(b =>
                        b.Staff_Group == staffGroup &&
                        b.PaymentMonth.ToLower() == month.ToLower() &&
                        b.PaymentYear == year)
                    .OrderBy(b => b.FullName)
                    .ToList();

                if (backupPayrolls.Count > 0)
                {
                    payrolls = backupPayrolls.Select(backupRecord => new Payroll
                    {
                        Id = backupRecord.Id,
                        FileNo = backupRecord.FileNo,
                        StaffID = backupRecord.StaffID,
                        FullName = backupRecord.FullName,
                        Staff_Group = backupRecord.Staff_Group,
                        GradeLevel = backupRecord.GradeLevel,
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
                        Deductions = backupRecord.Deductions,
                        Gross_Pay = backupRecord.Gross_Pay,
                        Net_Pay = backupRecord.Net_Pay,
                        Bank_Name = backupRecord.Bank_Name,
                        Bank_Branch = backupRecord.Bank_Branch,
                        Account_Number = backupRecord.Account_Number,
                        Sort_Code = backupRecord.Sort_Code
                    }).ToList();
                }
            }

            if (payrolls.Count == 0)
            {
                var model = new PaySlipViewModel
                {
                    Months = GetMonths(),
                    Years = GetYears()
                };

                ViewBag.GroupMessage = "No record found";

                return View("Payslip", model);
            }

            ViewBag.StaffGroup = staffGroup;
            ViewBag.Month = month.ToUpper();
            ViewBag.Year = year;

            return View("GroupPayslipResult", payrolls);
        }

        // =========================================================
        // EXPORT GROUP PAYSLIPS PDF (Updated to check Payroll & BackupPayroll)
        // =========================================================

        public IActionResult ExportGroupPayslipPdf(string staffGroup, string month, int year)
        {
            var payrolls = _context.Payroll
                .Where(x =>
                    x.Staff_Group == staffGroup &&
                    x.PaymentMonth.ToLower() == month.ToLower() &&
                    x.PaymentYear == year)
                .OrderBy(x => x.FullName)
                .ToList();

            if (payrolls.Count == 0)
            {
                var backupPayrolls = _context.BackupPayroll
                    .Where(b =>
                        b.Staff_Group == staffGroup &&
                        b.PaymentMonth.ToLower() == month.ToLower() &&
                        b.PaymentYear == year)
                    .OrderBy(b => b.FullName)
                    .ToList();

                if (backupPayrolls.Count > 0)
                {
                    payrolls = backupPayrolls.Select(backupRecord => new Payroll
                    {
                        Id = backupRecord.Id,
                        FileNo = backupRecord.FileNo,
                        StaffID = backupRecord.StaffID,
                        FullName = backupRecord.FullName,
                        Staff_Group = backupRecord.Staff_Group,
                        GradeLevel = backupRecord.GradeLevel,
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
                        Deductions = backupRecord.Deductions,
                        Gross_Pay = backupRecord.Gross_Pay,
                        Net_Pay = backupRecord.Net_Pay,
                        Bank_Name = backupRecord.Bank_Name,
                        Bank_Branch = backupRecord.Bank_Branch,
                        Account_Number = backupRecord.Account_Number,
                        Sort_Code = backupRecord.Sort_Code
                    }).ToList();
                }
            }

            if (payrolls.Count == 0)
            {
                return Content("No records found for the selected group, month, and year.");
            }

            ViewBag.StaffGroup = staffGroup;
            ViewBag.Month = month.ToUpper();
            ViewBag.Year = year;

            return new ViewAsPdf("GroupPayslipResult", payrolls)
            {
                FileName = $"GroupPayslips_{staffGroup.Replace(" ", "_")}_{month}_{year}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                CustomSwitches = "--disable-smart-shrinking --page-offset 0 --footer-center \"Page [page] of [topage]\" --footer-font-size \"8\""
            };
        }

        // =========================================================
        // PAY SUMMARY PAGE
        // =========================================================

        [HttpGet]
        public IActionResult Summary()
        {
            ViewBag.Months = GetMonths();
            ViewBag.Years = GetYears();

            return View();
        }

        // =========================================================
        // SEARCH PAY SUMMARY
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SearchSummary(string month, int year)
        {
            ViewBag.Months = GetMonths();
            ViewBag.Years = GetYears();

            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                ViewBag.Message = "No record found";
                return View("Summary");
            }

            ViewBag.Month = month?.ToUpper();
            ViewBag.Year = year;

            SetSummaryTotals(records);

            return View("SummaryResult", records);
        }

        // =========================================================
        // EXPORT SUMMARY EXCEL
        // =========================================================

        public IActionResult ExportSummaryExcel(string month, int year)
        {
            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                return Content("No record found");
            }

            ExcelPackage.License.SetNonCommercialPersonal("PayrollSystem");

            using var package = new ExcelPackage();

            var ws = package.Workbook.Worksheets.Add("PaySummary");

            ws.Cells[1, 1].Value = "UNIVERSITY OF BENIN TEACHING HOSPITAL";
            ws.Cells[2, 1].Value = $"PAYROLL SUMMARY REPORT FOR {month.ToUpper()} {year}";

            string[] headers =
            {
                "S/N", "Staff ID", "Full Name", "Staff Group", "Grade Level",
                "Consolidated", "Allowance", "Teaching Allowance", "Call Duty", "Hazard",
                "Shift", "Specialist", "Rural Posting", "Accoutrement", "Clinical Duty",
                "Non Clinical", "Pharmacist Consult", "Arrears", "Gross Pay", "Refund",
                "Overpayment", "Pension", "Tax", "Tax Arrears", "AMLSN",
                "NMA Dues", "NMA Special Levy", "MEDCAN Dues", "ARD", "NARD",
                "Rent", "Rent Arrears", "Deductions", "Net Pay", "Bank Name",
                "Account Number", "Sort Code"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[4, i + 1].Value = headers[i];
            }

            int row = 5;
            int sn = 1;

            foreach (var item in records)
            {
                int c = 1;

                ws.Cells[row, c++].Value = sn++;
                ws.Cells[row, c++].Value = item.StaffID;
                ws.Cells[row, c++].Value = item.FullName;
                ws.Cells[row, c++].Value = item.Staff_Group;
                ws.Cells[row, c++].Value = item.GradeLevel;

                ws.Cells[row, c++].Value = item.Consolidated;
                ws.Cells[row, c++].Value = item.Allowance;
                ws.Cells[row, c++].Value = item.TeachingAllowance;
                ws.Cells[row, c++].Value = item.CallDutyAllowance;
                ws.Cells[row, c++].Value = item.HazardAllowance;
                ws.Cells[row, c++].Value = item.ShiftAllowance;
                ws.Cells[row, c++].Value = item.SpecialistAllowance;
                ws.Cells[row, c++].Value = item.RuralPostingAllowance;
                ws.Cells[row, c++].Value = item.AccoutrementAllowance;
                ws.Cells[row, c++].Value = item.ConmessClinicalDutyAllowance;
                ws.Cells[row, c++].Value = item.ConmessNonClinicalAllowance;
                ws.Cells[row, c++].Value = item.PharmacistConsultAllowance;
                ws.Cells[row, c++].Value = item.Arrears;

                ws.Cells[row, c++].Value = item.Gross_Pay;

                ws.Cells[row, c++].Value = item.Refund;
                ws.Cells[row, c++].Value = item.OverPaymentDeduction;
                ws.Cells[row, c++].Value = item.Pension;
                ws.Cells[row, c++].Value = item.Tax;
                ws.Cells[row, c++].Value = item.TAX_ARREARS;
                ws.Cells[row, c++].Value = item.AMLSN;
                ws.Cells[row, c++].Value = item.NMA_DUES;
                ws.Cells[row, c++].Value = item.NMA_SPECIAL_LEVY;
                ws.Cells[row, c++].Value = item.MEDCAN_DUES;
                ws.Cells[row, c++].Value = item.ARD;
                ws.Cells[row, c++].Value = item.NARD;
                ws.Cells[row, c++].Value = item.RENT;
                ws.Cells[row, c++].Value = item.RENT_ARREARS;

                ws.Cells[row, c++].Value = item.Deductions;
                ws.Cells[row, c++].Value = item.Net_Pay;

                ws.Cells[row, c++].Value = item.Bank_Name;
                ws.Cells[row, c++].Value = item.Account_Number;
                ws.Cells[row, c++].Value = item.Sort_Code;

                row++;
            }

            ws.Cells[row, 5].Value = "TOTAL";

            int totalColumn = 6;

            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Consolidated);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Allowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.TeachingAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.CallDutyAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.HazardAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.ShiftAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.SpecialistAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.RuralPostingAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.AccoutrementAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.ConmessClinicalDutyAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.ConmessNonClinicalAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.PharmacistConsultAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Arrears);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Gross_Pay);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Refund);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.OverPaymentDeduction);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Pension);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Tax);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.TAX_ARREARS);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.AMLSN);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.NMA_DUES);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.NMA_SPECIAL_LEVY);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.MEDCAN_DUES);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.ARD);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.NARD);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.RENT);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.RENT_ARREARS);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Deductions);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Net_Pay);

            ws.Cells.AutoFitColumns();

            return File(
                package.GetAsByteArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"PaySummary_{month}_{year}.xlsx");
        }

        // =========================================================
        // EXPORT SUMMARY PDF
        // =========================================================

        public IActionResult ExportSummaryPdf(string month, int year)
        {
            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                return Content("No record found");
            }

            ViewBag.Month = month?.ToUpper();
            ViewBag.Year = year;

            SetSummaryTotals(records);

            return new ViewAsPdf("SummaryPdf", records)
            {
                FileName = $"PaySummary_{month}_{year}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape
            };
        }

        // =========================================================
        // EXPORT NET SUMMARY PDF
        // =========================================================

        [HttpGet]
        public IActionResult ExportNetSummaryPdf(string month, int year)
        {
            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                return Content("No record found");
            }

            ViewBag.Month = month?.ToUpper();
            ViewBag.Year = year;
            ViewBag.TotalNetPay = records.Sum(x => x.Net_Pay);

            return new ViewAsPdf("NetSummaryResult", records)
            {
                FileName = $"NetSummary_{month}_{year}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait
            };
        }


        // =========================================================
        // EARNINGS SUMMARY ACTIONS
        // =========================================================

        [HttpGet]
        public IActionResult EarningsSummary()
        {
            ViewBag.Months = GetMonths();
            ViewBag.Years = GetYears();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SearchEarningsSummary(string month, int year)
        {
            ViewBag.Months = GetMonths();
            ViewBag.Years = GetYears();

            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                ViewBag.Message = "No record found";
                return View("EarningsSummary");
            }

            ViewBag.Month = month.ToUpper();
            ViewBag.Year = year;

            SetEarningsSummaryTotals(records);

            return View("EarningsSummaryResult", records);
        }

        public IActionResult ExportEarningsSummaryExcel(string month, int year)
        {
            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                return Content("No record found");
            }

            ExcelPackage.License.SetNonCommercialPersonal("PayrollSystem");

            using var package = new ExcelPackage();

            var ws = package.Workbook.Worksheets.Add("EarningsSummary");

            ws.Cells[1, 1].Value = "UNIVERSITY OF BENIN TEACHING HOSPITAL";
            ws.Cells[2, 1].Value = $"EARNINGS SUMMARY REPORT FOR {month.ToUpper()} {year}";

            string[] headers =
            {
                "S/N", "Staff ID", "Full Name", "Staff Group", "Grade Level",
                "Consolidated", "Allowance", "Teaching Allowance", "Call Duty", "Hazard",
                "Shift", "Specialist", "Rural Posting", "Accoutrement", "Clinical Duty",
                "Non Clinical", "Pharmacist Consult", "Arrears", "Gross Pay"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[4, i + 1].Value = headers[i];
            }

            int row = 5;
            int sn = 1;

            foreach (var item in records)
            {
                int c = 1;

                ws.Cells[row, c++].Value = sn++;
                ws.Cells[row, c++].Value = item.StaffID;
                ws.Cells[row, c++].Value = item.FullName;
                ws.Cells[row, c++].Value = item.Staff_Group;
                ws.Cells[row, c++].Value = item.GradeLevel;

                ws.Cells[row, c++].Value = item.Consolidated;
                ws.Cells[row, c++].Value = item.Allowance;
                ws.Cells[row, c++].Value = item.TeachingAllowance;
                ws.Cells[row, c++].Value = item.CallDutyAllowance;
                ws.Cells[row, c++].Value = item.HazardAllowance;
                ws.Cells[row, c++].Value = item.ShiftAllowance;
                ws.Cells[row, c++].Value = item.SpecialistAllowance;
                ws.Cells[row, c++].Value = item.RuralPostingAllowance;
                ws.Cells[row, c++].Value = item.AccoutrementAllowance;
                ws.Cells[row, c++].Value = item.ConmessClinicalDutyAllowance;
                ws.Cells[row, c++].Value = item.ConmessNonClinicalAllowance;
                ws.Cells[row, c++].Value = item.PharmacistConsultAllowance;
                ws.Cells[row, c++].Value = item.Arrears;
                ws.Cells[row, c++].Value = item.Gross_Pay;

                row++;
            }

            ws.Cells[row, 5].Value = "TOTAL";

            int totalColumn = 6;

            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Consolidated);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Allowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.TeachingAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.CallDutyAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.HazardAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.ShiftAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.SpecialistAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.RuralPostingAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.AccoutrementAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.ConmessClinicalDutyAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.ConmessNonClinicalAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.PharmacistConsultAllowance);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Arrears);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Gross_Pay);

            ws.Cells.AutoFitColumns();

            return File(
                package.GetAsByteArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"EarningsSummary_{month}_{year}.xlsx");
        }

        // =========================================================
        // EXPORT EARNINGS SUMMARY PDF
        // =========================================================

        [HttpGet]
        public IActionResult ExportEarningsSummaryPdf(string month, int year)
        {
            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                return Content("No record found");
            }

            ViewBag.Month = month?.ToUpper();
            ViewBag.Year = year;

            SetEarningsSummaryTotals(records);

            return new ViewAsPdf("EarningsSummaryPdf", records)
            {
                FileName = $"EarningsSummary_{month}_{year}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape
            };
        }

        private void SetEarningsSummaryTotals(List<Payroll> records)
        {
            ViewBag.TotalConsolidated = records.Sum(x => x.Consolidated);
            ViewBag.TotalAllowance = records.Sum(x => x.Allowance);
            ViewBag.TotalTeachingAllowance = records.Sum(x => x.TeachingAllowance);
            ViewBag.TotalCallDutyAllowance = records.Sum(x => x.CallDutyAllowance);
            ViewBag.TotalHazardAllowance = records.Sum(x => x.HazardAllowance);
            ViewBag.TotalShiftAllowance = records.Sum(x => x.ShiftAllowance);
            ViewBag.TotalSpecialistAllowance = records.Sum(x => x.SpecialistAllowance);
            ViewBag.TotalRuralPostingAllowance = records.Sum(x => x.RuralPostingAllowance);
            ViewBag.TotalAccoutrementAllowance = records.Sum(x => x.AccoutrementAllowance);
            ViewBag.TotalConmessClinicalDutyAllowance = records.Sum(x => x.ConmessClinicalDutyAllowance);
            ViewBag.TotalConmessNonClinicalAllowance = records.Sum(x => x.ConmessNonClinicalAllowance);
            ViewBag.TotalPharmacistConsultAllowance = records.Sum(x => x.PharmacistConsultAllowance);
            ViewBag.TotalArrears = records.Sum(x => x.Arrears);
            ViewBag.TotalGrossPay = records.Sum(x => x.Gross_Pay);
        }

        // =========================================================
        // DEDUCTION SUMMARY ACTIONS
        // =========================================================

        [HttpGet]
        public IActionResult DeductionSummary()
        {
            ViewBag.Months = GetMonths();
            ViewBag.Years = GetYears();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SearchDeductionSummary(string month, int year)
        {
            ViewBag.Months = GetMonths();
            ViewBag.Years = GetYears();

            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                ViewBag.Message = "No record found";
                return View("DeductionSummary");
            }

            ViewBag.Month = month.ToUpper();
            ViewBag.Year = year;

            SetDeductionSummaryTotals(records);

            return View("DeductionSummaryResult", records);
        }

        public IActionResult ExportDeductionSummaryExcel(string month, int year)
        {
            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                return Content("No record found");
            }

            ExcelPackage.License.SetNonCommercialPersonal("PayrollSystem");

            using var package = new ExcelPackage();

            var ws = package.Workbook.Worksheets.Add("DeductionSummary");

            ws.Cells[1, 1].Value = "UNIVERSITY OF BENIN TEACHING HOSPITAL";
            ws.Cells[2, 1].Value = $"DEDUCTION SUMMARY REPORT FOR {month.ToUpper()} {year}";

            string[] headers =
            {
                "S/N", "Staff ID", "Full Name", "Staff Group", "Grade Level",
                "Refund", "Overpayment", "Pension", "Tax", "Tax Arrears", "AMLSN",
                "NMA Dues", "NMA Special Levy", "MEDCAN Dues", "ARD", "NARD",
                "Rent", "Rent Arrears", "Deductions"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[4, i + 1].Value = headers[i];
            }

            int row = 5;
            int sn = 1;

            foreach (var item in records)
            {
                int c = 1;

                ws.Cells[row, c++].Value = sn++;
                ws.Cells[row, c++].Value = item.StaffID;
                ws.Cells[row, c++].Value = item.FullName;
                ws.Cells[row, c++].Value = item.Staff_Group;
                ws.Cells[row, c++].Value = item.GradeLevel;

                ws.Cells[row, c++].Value = item.Refund;
                ws.Cells[row, c++].Value = item.OverPaymentDeduction;
                ws.Cells[row, c++].Value = item.Pension;
                ws.Cells[row, c++].Value = item.Tax;
                ws.Cells[row, c++].Value = item.TAX_ARREARS;
                ws.Cells[row, c++].Value = item.AMLSN;
                ws.Cells[row, c++].Value = item.NMA_DUES;
                ws.Cells[row, c++].Value = item.NMA_SPECIAL_LEVY;
                ws.Cells[row, c++].Value = item.MEDCAN_DUES;
                ws.Cells[row, c++].Value = item.ARD;
                ws.Cells[row, c++].Value = item.NARD;
                ws.Cells[row, c++].Value = item.RENT;
                ws.Cells[row, c++].Value = item.RENT_ARREARS;
                ws.Cells[row, c++].Value = item.Deductions;

                row++;
            }

            ws.Cells[row, 5].Value = "TOTAL";

            int totalColumn = 6;

            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Refund);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.OverPaymentDeduction);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Pension);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Tax);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.TAX_ARREARS);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.AMLSN);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.NMA_DUES);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.NMA_SPECIAL_LEVY);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.MEDCAN_DUES);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.ARD);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.NARD);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.RENT);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.RENT_ARREARS);
            ws.Cells[row, totalColumn++].Value = records.Sum(x => x.Deductions);

            ws.Cells.AutoFitColumns();

            return File(
                package.GetAsByteArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DeductionSummary_{month}_{year}.xlsx");
        }

        // =========================================================
        // EXPORT DEDUCTION SUMMARY PDF
        // =========================================================

        [HttpGet]
        public IActionResult ExportDeductionSummaryPdf(string month, int year)
        {
            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                return Content("No record found");
            }

            ViewBag.Month = month?.ToUpper();
            ViewBag.Year = year;

            SetDeductionSummaryTotals(records);

            return new ViewAsPdf("DeductionSummaryPdf", records)
            {
                FileName = $"DeductionSummary_{month}_{year}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape
            };
        }

        private void SetDeductionSummaryTotals(List<Payroll> records)
        {
            ViewBag.TotalRefund = records.Sum(x => x.Refund);
            ViewBag.TotalOverPaymentDeduction = records.Sum(x => x.OverPaymentDeduction);
            ViewBag.TotalPension = records.Sum(x => x.Pension);
            ViewBag.TotalTax = records.Sum(x => x.Tax);
            ViewBag.TotalTaxArrears = records.Sum(x => x.TAX_ARREARS);
            ViewBag.TotalAMLSN = records.Sum(x => x.AMLSN);
            ViewBag.TotalNmaDues = records.Sum(x => x.NMA_DUES);
            ViewBag.TotalNmaSpecialLevy = records.Sum(x => x.NMA_SPECIAL_LEVY);
            ViewBag.TotalMedcanDues = records.Sum(x => x.MEDCAN_DUES);
            ViewBag.TotalARD = records.Sum(x => x.ARD);
            ViewBag.TotalNARD = records.Sum(x => x.NARD);
            ViewBag.TotalRent = records.Sum(x => x.RENT);
            ViewBag.TotalRentArrears = records.Sum(x => x.RENT_ARREARS);
            ViewBag.TotalDeductions = records.Sum(x => x.Deductions);
        }

        // =========================================================
        // NET PAY SUMMARY ACTIONS
        // =========================================================

        [HttpGet]
        public IActionResult NetSummary()
        {
            ViewBag.Months = GetMonths();
            ViewBag.Years = GetYears();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SearchNetSummary(string month, int year)
        {
            ViewBag.Months = GetMonths();
            ViewBag.Years = GetYears();

            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                ViewBag.Message = "No record found";
                return View("NetSummary");
            }

            ViewBag.Month = month?.ToUpper();
            ViewBag.Year = year;
            ViewBag.TotalNetPay = records.Sum(x => x.Net_Pay);

            return View("NetSummaryResult", records);
        }

        public IActionResult ExportNetSummaryExcel(string month, int year)
        {
            var records = BuildSummary(month, year);

            if (records.Count == 0)
            {
                return Content("No record found");
            }

            string descriptionText = $"UBTH {month.ToUpper()} {year} SALARY";

            ExcelPackage.License.SetNonCommercialPersonal("PayrollSystem");

            using var package = new ExcelPackage();

            var ws = package.Workbook.Worksheets.Add("NetSummary");

            ws.Cells[1, 1].Value = "UNIVERSITY OF BENIN TEACHING HOSPITAL";
            ws.Cells[2, 1].Value = $"NET PAY SUMMARY REPORT FOR {month.ToUpper()} {year}";

            string[] headers =
            {
                "S/N", "Staff ID", "Full Name", "Staff Group", "Grade Level",
                "Description", "Net Pay", "Bank Name", "Account Number", "Sort Code"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[4, i + 1].Value = headers[i];
            }

            int row = 5;
            int sn = 1;

            foreach (var item in records)
            {
                int c = 1;

                ws.Cells[row, c++].Value = sn++;
                ws.Cells[row, c++].Value = item.StaffID;
                ws.Cells[row, c++].Value = item.FullName;
                ws.Cells[row, c++].Value = item.Staff_Group;
                ws.Cells[row, c++].Value = item.GradeLevel;
                ws.Cells[row, c++].Value = descriptionText;
                ws.Cells[row, c++].Value = item.Net_Pay;
                ws.Cells[row, c++].Value = item.Bank_Name;
                ws.Cells[row, c++].Value = item.Account_Number;
                ws.Cells[row, c++].Value = item.Sort_Code;

                row++;
            }

            ws.Cells[row, 6].Value = "TOTAL";
            ws.Cells[row, 7].Value = records.Sum(x => x.Net_Pay);

            ws.Cells.AutoFitColumns();

            return File(
                package.GetAsByteArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"NetSummary_{month}_{year}.xlsx");
        }

        // =========================================================
        // JOURNAL REPORT PAGE
        // =========================================================

        [HttpGet]
        public IActionResult JournalReport()
        {
            ViewBag.Months = GetMonths();
            ViewBag.Years = GetYears();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerateJournalReport(string month, int year)
        {
            var model = BuildJournalReport(month, year);

            if (model == null)
            {
                ViewBag.Message = "No record found";
                ViewBag.Months = GetMonths();
                ViewBag.Years = GetYears();

                return View("JournalReport");
            }

            return View("JournalReportResult", model);
        }

        public IActionResult ExportJournalPdf(string month, int year)
        {
            var model = BuildJournalReport(month, year);

            if (model == null)
            {
                return Content("No record found");
            }

            return new ViewAsPdf("JournalReportPdf", model)
            {
                FileName = $"JournalReport_{month}_{year}.pdf",
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        public IActionResult ExportJournalExcel(string month, int year)
        {
            var model = BuildJournalReport(month, year);

            if (model == null)
            {
                return Content("No record found");
            }

            ExcelPackage.License.SetNonCommercialPersonal("PayrollSystem");

            using var package = new ExcelPackage();

            var worksheet = package.Workbook.Worksheets.Add("JournalReport");

            worksheet.Cells[1, 1].Value = "UNIVERSITY OF BENIN TEACHING HOSPITAL";
            worksheet.Cells[2, 1].Value = $"PAYROLL JOURNAL FOR THE MONTH OF {month.ToUpper()}, {year}";

            worksheet.Cells[4, 1].Value = "S/N";
            worksheet.Cells[4, 2].Value = "DESCRIPTION";
            worksheet.Cells[4, 3].Value = "TOTAL STAFF";
            worksheet.Cells[4, 4].Value = "DEBIT";
            worksheet.Cells[4, 5].Value = "CREDIT";

            int row = 5;

            foreach (var item in model.JournalLines)
            {
                worksheet.Cells[row, 1].Value = item.SN;
                worksheet.Cells[row, 2].Value = item.Description;
                worksheet.Cells[row, 3].Value = item.TotalStaff;
                worksheet.Cells[row, 4].Value = item.Debit;
                worksheet.Cells[row, 5].Value = item.Credit;

                row++;
            }

            worksheet.Cells[row, 2].Value = "TOTAL";
            worksheet.Cells[row, 4].Value = model.TotalDebit;
            worksheet.Cells[row, 5].Value = model.TotalCredit;

            worksheet.Cells.AutoFitColumns();

            return File(
                package.GetAsByteArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"JournalReport_{month}_{year}.xlsx");
        }

        // =========================================================
        // BANK REPORT PAGE
        // =========================================================

        [HttpGet]
        public IActionResult BankReport()
        {
            ViewBag.Months = GetMonths();
            ViewBag.Years = GetYears();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerateBankReport(string month, int year)
        {
            var model = BuildBankReport(month, year);

            if (model == null)
            {
                ViewBag.Message = "No record found";
                ViewBag.Months = GetMonths();
                ViewBag.Years = GetYears();

                return View("BankReport");
            }

            return View("BankReportResult", model);
        }

        public IActionResult ExportBankReportPdf(string month, int year)
        {
            var model = BuildBankReport(month, year);

            if (model == null)
            {
                return Content("No record found");
            }

            return new ViewAsPdf("BankReportPdf", model)
            {
                FileName = $"BankReport_{month}_{year}.pdf",
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        public IActionResult ExportBankReportExcel(string month, int year)
        {
            var model = BuildBankReport(month, year);

            if (model == null)
            {
                return Content("No record found");
            }

            ExcelPackage.License.SetNonCommercialPersonal("PayrollSystem");

            using var package = new ExcelPackage();

            var worksheet = package.Workbook.Worksheets.Add("BankReport");

            worksheet.Cells[1, 1].Value = "UNIVERSITY OF BENIN TEACHING HOSPITAL";
            worksheet.Cells[2, 1].Value = $"BANK PAYMENT SUMMARY FOR THE MONTH OF {month.ToUpper()} - {year}";

            worksheet.Cells[4, 1].Value = "S/N";
            worksheet.Cells[4, 2].Value = "BANK CODE";
            worksheet.Cells[4, 3].Value = "BANK NAME";
            worksheet.Cells[4, 4].Value = "BRANCH";
            worksheet.Cells[4, 5].Value = "NET PAY";
            worksheet.Cells[4, 6].Value = "NO OF STAFF";

            int row = 5;

            foreach (var item in model.BankLines)
            {
                worksheet.Cells[row, 1].Value = item.SN;
                worksheet.Cells[row, 2].Value = item.BankCode;
                worksheet.Cells[row, 3].Value = item.BankName;
                worksheet.Cells[row, 4].Value = item.Branch;
                worksheet.Cells[row, 5].Value = item.NetPay;
                worksheet.Cells[row, 6].Value = item.NoOfStaff;

                row++;
            }

            worksheet.Cells[row, 4].Value = "TOTAL";
            worksheet.Cells[row, 5].Value = model.TotalNetPay;
            worksheet.Cells[row, 6].Value = model.TotalStaff;

            worksheet.Cells.AutoFitColumns();

            return File(
                package.GetAsByteArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BankReport_{month}_{year}.xlsx");
        }

        // =========================================================
        // CREATE NEW MONTH PAYROLL
        // =========================================================

        [HttpGet]
        public IActionResult CreateMonth()
        {
            return View("CreateMonthAccess");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyAccessCode(string accessCode)
        {
            if (accessCode != "654321")
            {
                ViewBag.ErrorMessage = "Access Denied";
                return View("CreateMonthAccess");
            }

            var model = new PaySlipViewModel
            {
                Months = GetMonths(),
                Years = GetYears()
            };

            return View("CreateMonthForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessCreateMonth(string selectedMonth, int selectedYear)
        {
            if (string.IsNullOrWhiteSpace(selectedMonth) || selectedYear <= 0)
            {
                ModelState.AddModelError("", "Please select a valid Month and Year.");
                var model = new PaySlipViewModel
                {
                    Months = GetMonths(),
                    Years = GetYears()
                };
                return View("CreateMonthForm", model);
            }

            using var transaction = _context.Database.BeginTransaction();

            try
            {
                var payrollRecords = _context.Payroll.ToList();

                if (payrollRecords.Count > 0)
                {
                    foreach (var record in payrollRecords)
                    {
                        var backup = new BackupPayroll
                        {
                            FileNo = record.FileNo,
                            StaffID = record.StaffID,
                            FullName = record.FullName,
                            Staff_Group = record.Staff_Group,
                            GradeLevel = record.GradeLevel,
                            PaymentMonth = record.PaymentMonth,
                            PaymentYear = record.PaymentYear,
                            Consolidated = record.Consolidated,
                            Allowance = record.Allowance,
                            TeachingAllowance = record.TeachingAllowance,
                            CallDutyAllowance = record.CallDutyAllowance,
                            HazardAllowance = record.HazardAllowance,
                            ShiftAllowance = record.ShiftAllowance,
                            SpecialistAllowance = record.SpecialistAllowance,
                            RuralPostingAllowance = record.RuralPostingAllowance,
                            AccoutrementAllowance = record.AccoutrementAllowance,
                            ConmessClinicalDutyAllowance = record.ConmessClinicalDutyAllowance,
                            ConmessNonClinicalAllowance = record.ConmessNonClinicalAllowance,
                            PharmacistConsultAllowance = record.PharmacistConsultAllowance,
                            Arrears = record.Arrears,
                            Refund = record.Refund,
                            OverPaymentDeduction = record.OverPaymentDeduction,
                            Pension = record.Pension,
                            Tax = record.Tax,
                            TAX_ARREARS = record.TAX_ARREARS,
                            AMLSN = record.AMLSN,
                            NMA_DUES = record.NMA_DUES,
                            NMA_SPECIAL_LEVY = record.NMA_SPECIAL_LEVY,
                            MEDCAN_DUES = record.MEDCAN_DUES,
                            ARD = record.ARD,
                            NARD = record.NARD,
                            RENT = record.RENT,
                            RENT_ARREARS = record.RENT_ARREARS,
                            Deductions = record.Deductions,
                            Gross_Pay = record.Gross_Pay,
                            Net_Pay = record.Net_Pay,
                            Bank_Name = record.Bank_Name,
                            Bank_Branch = record.Bank_Branch,
                            Account_Number = record.Account_Number,
                            Sort_Code = record.Sort_Code
                        };
                        _context.BackupPayroll.Add(backup);
                    }

                    foreach (var record in payrollRecords)
                    {
                        record.PaymentMonth = selectedMonth;
                        record.PaymentYear = selectedYear;
                    }

                    _context.SaveChanges();
                }

                transaction.Commit();

                ViewBag.SuccessMessage = "Creating New Month Payroll Processing Complete.";
                return View("CreateMonthComplete");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ModelState.AddModelError("", "An error occurred while creating new month payroll: " + ex.Message);
                var model = new PaySlipViewModel
                {
                    Months = GetMonths(),
                    Years = GetYears()
                };
                return View("CreateMonthForm", model);
            }
        }

        // =========================================================
        // HELPER FUNCTIONS
        // =========================================================

        private List<Payroll> BuildSummary(string month, int year)
        {
            if (string.IsNullOrWhiteSpace(month)) return new List<Payroll>();

            return _context.Payroll
                .Where(x => x.PaymentMonth != null && x.PaymentMonth.ToLower() == month.ToLower() && x.PaymentYear == year)
                .OrderBy(x => x.FullName)
                .ToList();
        }

        private BankReportViewModel BuildBankReport(string month, int year)
        {
            var payrollRecords = _context.Payroll
                .Where(x => x.PaymentMonth == month && x.PaymentYear == year)
                .ToList();

            if (payrollRecords.Count == 0) return null;

            var model = new BankReportViewModel { Month = month.ToUpper(), Year = year };

            int sn = 1;
            var groupedBanks = payrollRecords.GroupBy(x => x.Bank_Name).OrderBy(x => x.Key).ToList();

            foreach (var bank in groupedBanks)
            {
                var firstRecord = bank.FirstOrDefault();
                model.BankLines.Add(new BankReportLine
                {
                    SN = sn++,
                    BankCode = GetBankCode(bank.Key),
                    BankName = bank.Key,
                    Branch = firstRecord?.Bank_Branch ?? "BENIN",
                    NetPay = bank.Sum(x => x.Net_Pay),
                    NoOfStaff = bank.Count()
                });
            }

            model.TotalNetPay = model.BankLines.Sum(x => x.NetPay);
            model.TotalStaff = model.BankLines.Sum(x => x.NoOfStaff);

            return model;
        }

        private JournalReportViewModel BuildJournalReport(string month, int year)
        {
            var payrollRecords = _context.Payroll
                .Where(x => x.PaymentMonth == month && x.PaymentYear == year)
                .ToList();

            if (payrollRecords.Count == 0) return null;

            var model = new JournalReportViewModel
            {
                Month = month.ToUpper(),
                Year = year,
                JournalLines = new List<JournalLine>()
            };

            int sn = 1;
            void AddLine(string description, int totalStaff, decimal debit, decimal credit)
            {
                if (debit != 0 || credit != 0)
                {
                    model.JournalLines.Add(new JournalLine
                    {
                        SN = sn++,
                        Description = description,
                        TotalStaff = totalStaff,
                        Debit = debit,
                        Credit = credit
                    });
                }
            }

            AddLine("CONSOLIDATED SALARY", payrollRecords.Count(x => x.Consolidated > 0), payrollRecords.Sum(x => x.Consolidated), 0);
            AddLine("ALLOWANCE", payrollRecords.Count(x => x.Allowance > 0), payrollRecords.Sum(x => x.Allowance), 0);
            AddLine("TEACHING ALLOWANCE", payrollRecords.Count(x => x.TeachingAllowance > 0), payrollRecords.Sum(x => x.TeachingAllowance), 0);
            AddLine("CALL DUTY ALLOWANCE", payrollRecords.Count(x => x.CallDutyAllowance > 0), payrollRecords.Sum(x => x.CallDutyAllowance), 0);
            AddLine("HAZARD ALLOWANCE", payrollRecords.Count(x => x.HazardAllowance > 0), payrollRecords.Sum(x => x.HazardAllowance), 0);
            AddLine("SHIFT ALLOWANCE", payrollRecords.Count(x => x.ShiftAllowance > 0), payrollRecords.Sum(x => x.ShiftAllowance), 0);
            AddLine("SPECIALIST ALLOWANCE", payrollRecords.Count(x => x.SpecialistAllowance > 0), payrollRecords.Sum(x => x.SpecialistAllowance), 0);
            AddLine("RURAL POSTING ALLOWANCE", payrollRecords.Count(x => x.RuralPostingAllowance > 0), payrollRecords.Sum(x => x.RuralPostingAllowance), 0);
            AddLine("ACCOUTREMENT ALLOWANCE", payrollRecords.Count(x => x.AccoutrementAllowance > 0), payrollRecords.Sum(x => x.AccoutrementAllowance), 0);
            AddLine("CONMESS CLINICAL DUTY ALLOWANCE", payrollRecords.Count(x => x.ConmessClinicalDutyAllowance > 0), payrollRecords.Sum(x => x.ConmessClinicalDutyAllowance), 0);
            AddLine("CONMESS NON CLINICAL ALLOWANCE", payrollRecords.Count(x => x.ConmessNonClinicalAllowance > 0), payrollRecords.Sum(x => x.ConmessNonClinicalAllowance), 0);
            AddLine("PHARMACIST CONSULT ALLOWANCE", payrollRecords.Count(x => x.PharmacistConsultAllowance > 0), payrollRecords.Sum(x => x.PharmacistConsultAllowance), 0);
            AddLine("ARREARS", payrollRecords.Count(x => x.Arrears > 0), payrollRecords.Sum(x => x.Arrears), 0);

            AddLine("REFUND", payrollRecords.Count(x => x.Refund > 0), 0, payrollRecords.Sum(x => x.Refund));
            AddLine("PENSION", payrollRecords.Count(x => x.Pension > 0), 0, payrollRecords.Sum(x => x.Pension));
            AddLine("TAX", payrollRecords.Count(x => x.Tax > 0), 0, payrollRecords.Sum(x => x.Tax));
            AddLine("TAX ARREARS", payrollRecords.Count(x => x.TAX_ARREARS > 0), 0, payrollRecords.Sum(x => x.TAX_ARREARS));
            AddLine("OVER PAYMENT DEDUCTION", payrollRecords.Count(x => x.OverPaymentDeduction > 0), 0, payrollRecords.Sum(x => x.OverPaymentDeduction));
            AddLine("AMLSN", payrollRecords.Count(x => x.AMLSN > 0), 0, payrollRecords.Sum(x => x.AMLSN));
            AddLine("NMA DUES", payrollRecords.Count(x => x.NMA_DUES > 0), 0, payrollRecords.Sum(x => x.NMA_DUES));
            AddLine("NMA SPECIAL LEVY", payrollRecords.Count(x => x.NMA_SPECIAL_LEVY > 0), 0, payrollRecords.Sum(x => x.NMA_SPECIAL_LEVY));
            AddLine("MEDCAN DUES", payrollRecords.Count(x => x.MEDCAN_DUES > 0), 0, payrollRecords.Sum(x => x.MEDCAN_DUES));
            AddLine("ARD", payrollRecords.Count(x => x.ARD > 0), 0, payrollRecords.Sum(x => x.ARD));
            AddLine("NARD", payrollRecords.Count(x => x.NARD > 0), 0, payrollRecords.Sum(x => x.NARD));
            AddLine("RENT", payrollRecords.Count(x => x.RENT > 0), 0, payrollRecords.Sum(x => x.RENT));
            AddLine("RENT ARREARS", payrollRecords.Count(x => x.RENT_ARREARS > 0), 0, payrollRecords.Sum(x => x.RENT_ARREARS));

            AddLine("NET PAY", payrollRecords.Count, 0, payrollRecords.Sum(x => x.Net_Pay));

            model.TotalDebit = model.JournalLines.Sum(x => x.Debit);
            model.TotalCredit = model.JournalLines.Sum(x => x.Credit);

            return model;
        }

        private void RecalculatePayroll(Payroll payroll)
        {
            payroll.Deductions = CalculateTotalDeductions(payroll);
            payroll.Gross_Pay = CalculateGrossPay(payroll);
            payroll.Net_Pay = payroll.Gross_Pay - payroll.Deductions;
        }

        private decimal CalculateTotalDeductions(Payroll payroll)
        {
            return
                payroll.Refund +
                payroll.OverPaymentDeduction +
                payroll.Pension +
                payroll.Tax +
                payroll.TAX_ARREARS +
                payroll.AMLSN +
                payroll.NMA_DUES +
                payroll.NMA_SPECIAL_LEVY +
                payroll.MEDCAN_DUES +
                payroll.ARD +
                payroll.NARD +
                payroll.RENT +
                payroll.RENT_ARREARS;
        }

        private decimal CalculateGrossPay(Payroll payroll)
        {
            return payroll.Consolidated + payroll.Allowance + payroll.TeachingAllowance + payroll.CallDutyAllowance + payroll.HazardAllowance + payroll.ShiftAllowance + payroll.SpecialistAllowance + payroll.RuralPostingAllowance + payroll.AccoutrementAllowance + payroll.ConmessClinicalDutyAllowance + payroll.ConmessNonClinicalAllowance + payroll.PharmacistConsultAllowance + payroll.Arrears;
        }

        private void SetSummaryTotals(List<Payroll> records)
        {
            ViewBag.TotalConsolidated = records.Sum(x => x.Consolidated);
            ViewBag.TotalAllowance = records.Sum(x => x.Allowance);
            ViewBag.TotalTeachingAllowance = records.Sum(x => x.TeachingAllowance);
            ViewBag.TotalCallDutyAllowance = records.Sum(x => x.CallDutyAllowance);
            ViewBag.TotalHazardAllowance = records.Sum(x => x.HazardAllowance);
            ViewBag.TotalShiftAllowance = records.Sum(x => x.ShiftAllowance);
            ViewBag.TotalSpecialistAllowance = records.Sum(x => x.SpecialistAllowance);
            ViewBag.TotalRuralPostingAllowance = records.Sum(x => x.RuralPostingAllowance);
            ViewBag.TotalAccoutrementAllowance = records.Sum(x => x.AccoutrementAllowance);
            ViewBag.TotalConmessClinicalDutyAllowance = records.Sum(x => x.ConmessClinicalDutyAllowance);
            ViewBag.TotalConmessNonClinicalAllowance = records.Sum(x => x.ConmessNonClinicalAllowance);
            ViewBag.TotalPharmacistConsultAllowance = records.Sum(x => x.PharmacistConsultAllowance);
            ViewBag.TotalArrears = records.Sum(x => x.Arrears);
            ViewBag.TotalGrossPay = records.Sum(x => x.Gross_Pay);
            ViewBag.TotalRefund = records.Sum(x => x.Refund);
            ViewBag.TotalOverPaymentDeduction = records.Sum(x => x.OverPaymentDeduction);
            ViewBag.TotalPension = records.Sum(x => x.Pension);
            ViewBag.TotalTax = records.Sum(x => x.Tax);
            ViewBag.TotalTaxArrears = records.Sum(x => x.TAX_ARREARS);
            ViewBag.TotalAMLSN = records.Sum(x => x.AMLSN);
            ViewBag.TotalNmaDues = records.Sum(x => x.NMA_DUES);
            ViewBag.TotalNmaSpecialLevy = records.Sum(x => x.NMA_SPECIAL_LEVY);
            ViewBag.TotalMedcanDues = records.Sum(x => x.MEDCAN_DUES);
            ViewBag.TotalARD = records.Sum(x => x.ARD);
            ViewBag.TotalNARD = records.Sum(x => x.NARD);
            ViewBag.TotalRent = records.Sum(x => x.RENT);
            ViewBag.TotalRentArrears = records.Sum(x => x.RENT_ARREARS);
            ViewBag.TotalDeductions = records.Sum(x => x.Deductions);
            ViewBag.TotalNetPay = records.Sum(x => x.Net_Pay);
        }

        private List<SelectListItem> GetMonths()
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

        private List<SelectListItem> GetYears()
        {
            return Enumerable.Range(2026, 25).Select(y => new SelectListItem
            {
                Text = y.ToString(),
                Value = y.ToString()
            }).ToList();
        }

        private bool IsValidDeductionHead(string deductionHead)
        {
            string head = NormalizeDeductionHead(deductionHead);
            switch (head)
            {
                case "OVERPAYMENTDEDUCTION":
                case "PENSION":
                case "TAX":
                case "TAXARREARS":
                case "AMLSN":
                case "NMADUES":
                case "NMASPECIALLEVY":
                case "MEDCANDUES":
                case "ARD":
                case "NARD":
                case "RENT":
                case "RENTARREARS":
                    return true;
                default:
                    return false;
            }
        }

        private string NormalizeDeductionHead(string deductionHead)
        {
            return deductionHead.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
        }

        private void UpdateDeductionField(Payroll payroll, string deductionHead, decimal amount)
        {
            string head = NormalizeDeductionHead(deductionHead);

            switch (head)
            {
                case "OVERPAYMENTDEDUCTION": payroll.OverPaymentDeduction += amount; break;
                case "PENSION": payroll.Pension += amount; break;
                case "TAX": payroll.Tax += amount; break;
                case "TAXARREARS": payroll.TAX_ARREARS += amount; break;
                case "AMLSN": payroll.AMLSN += amount; break;
                case "NMADUES": payroll.NMA_DUES += amount; break;
                case "NMASPECIALLEVY": payroll.NMA_SPECIAL_LEVY += amount; break;
                case "MEDCANDUES": payroll.MEDCAN_DUES += amount; break;
                case "ARD": payroll.ARD += amount; break;
                case "NARD": payroll.NARD += amount; break;
                case "RENT": payroll.RENT += amount; break;
                case "RENTARREARS": payroll.RENT_ARREARS += amount; break;
                default: throw new ArgumentException($"Invalid deduction head: {deductionHead}");
            }
        }

        private string GetBankSortCode(string bankName)
        {
            if (string.IsNullOrWhiteSpace(bankName)) return "";

            switch (bankName.Trim().ToUpper())
            {
                case "ACCESS BANK PLC": return "044";
                case "FIDELITY BANK PLC": return "070";
                case "FIRST CITY MONUMENT BANK (FCMB)": return "214";
                case "FIRST BANK OF NIGERIA LIMITED": return "011";
                case "GUARANTY TRUST BANK (GTBANK)": return "058";
                case "UNITED BANK FOR AFRICA (UBA)": return "033";
                case "ZENITH BANK PLC": return "057";
                case "CITIBANK NIGERIA LIMITED": return "023";
                case "ECOBANK NIGERIA PLC": return "050";
                case "GLOBUS BANK LIMITED": return "00103";
                case "KEYSTONE BANK LIMITED": return "082";
                case "POLARIS BANK PLC": return "076";
                case "STANBIC IBTC BANK PLC": return "221";
                case "STANDARD CHARTERED BANK NIGERIA": return "068";
                case "STERLING BANK PLC": return "232";
                case "TITAN TRUST BANK LIMITED": return "102";
                case "UNION BANK OF NIGERIA PLC": return "032";
                case "UNITY BANK PLC": return "215";
                case "WEMA BANK PLC": return "035";
                case "JAIZ BANK PLC": return "301";
                case "PROVIDUS BANK LIMITED": return "101";
                case "SUNTRUST BANK": return "100";
                case "TAJ BANK": return "302";
                default: return "";
            }
        }

        [HttpGet]
        public IActionResult Deactivate() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SearchDeactivate(string staffId)
        {
            if (string.IsNullOrWhiteSpace(staffId))
            {
                ViewBag.Message = "Please enter Staff ID";
                return View("Deactivate");
            }

            var payroll = _context.Payroll.FirstOrDefault(x => x.StaffID == staffId.Trim());

            if (payroll == null)
            {
                ViewBag.Message = "No record found";
                return View("Deactivate");
            }

            return View("DeactivateResult", payroll);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeactivateStaff(int id)
        {
            var payroll = _context.Payroll.FirstOrDefault(x => x.Id == id);

            if (payroll == null)
            {
                ViewBag.Message = "No record found";
                return View("Deactivate");
            }

            using var transaction = _context.Database.BeginTransaction();

            try
            {
                _context.DeActivate.Add(CopyPayrollToDeactivate(payroll));
                _context.Payroll.Remove(payroll);
                _context.SaveChanges();

                transaction.Commit();

                TempData["Success"] = "Staff Payroll De-Activated";
                return RedirectToAction("Deactivate");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ViewBag.Message = $"Unable to deactivate staff: {ex.Message}";
                return View("Deactivate");
            }
        }

        [HttpGet]
        public IActionResult MultipleDeactivate() => View(new MultipleDeactivateViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessMultipleDeactivate(MultipleDeactivateViewModel model)
        {
            if (model == null) return View("MultipleDeactivate", new MultipleDeactivateViewModel());

            if (string.IsNullOrWhiteSpace(model.StaffIds))
            {
                ModelState.AddModelError("", "Please enter one or more Staff IDs.");
                return View("MultipleDeactivate", model);
            }

            var staffIds = model.StaffIds
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            model.TotalEntered = staffIds.Count;

            int found = 0, deactivated = 0;
            var notFound = new List<string>();

            using var transaction = _context.Database.BeginTransaction();

            try
            {
                foreach (string staffId in staffIds)
                {
                    var payroll = _context.Payroll.FirstOrDefault(x => x.StaffID == staffId);

                    if (payroll == null)
                    {
                        notFound.Add(staffId);
                        continue;
                    }

                    found++;
                    _context.DeActivate.Add(CopyPayrollToDeactivate(payroll));
                    _context.Payroll.Remove(payroll);
                    deactivated++;
                }

                _context.SaveChanges();
                transaction.Commit();

                model.TotalFound = found;
                model.TotalDeactivated = deactivated;
                model.TotalNotFound = notFound.Count;
                model.NotFoundStaffIds = notFound;

                TempData["Success"] = $"{deactivated} staff payroll record(s) were successfully de-activated.";

                return View("MultipleDeactivate", model);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ModelState.AddModelError("", "An error occurred while processing de-activation.");
                ModelState.AddModelError("", ex.Message);

                return View("MultipleDeactivate", model);
            }
        }

        private DeActivate CopyPayrollToDeactivate(Payroll payroll)
        {
            return new DeActivate
            {
                FileNo = payroll.FileNo,
                StaffID = payroll.StaffID,
                FullName = payroll.FullName,
                Staff_Group = payroll.Staff_Group,
                GradeLevel = payroll.GradeLevel,
                PaymentMonth = payroll.PaymentMonth,
                PaymentYear = payroll.PaymentYear,
                Consolidated = payroll.Consolidated,
                Allowance = payroll.Allowance,
                TeachingAllowance = payroll.TeachingAllowance,
                CallDutyAllowance = payroll.CallDutyAllowance,
                HazardAllowance = payroll.HazardAllowance,
                ShiftAllowance = payroll.ShiftAllowance,
                SpecialistAllowance = payroll.SpecialistAllowance,
                RuralPostingAllowance = payroll.RuralPostingAllowance,
                AccoutrementAllowance = payroll.AccoutrementAllowance,
                ConmessClinicalDutyAllowance = payroll.ConmessClinicalDutyAllowance,
                ConmessNonClinicalAllowance = payroll.ConmessNonClinicalAllowance,
                PharmacistConsultAllowance = payroll.PharmacistConsultAllowance,
                Arrears = payroll.Arrears,
                Refund = payroll.Refund,
                OverPaymentDeduction = payroll.OverPaymentDeduction,
                Pension = payroll.Pension,
                Tax = payroll.Tax,
                TAX_ARREARS = payroll.TAX_ARREARS,
                AMLSN = payroll.AMLSN,
                NMA_DUES = payroll.NMA_DUES,
                NMA_SPECIAL_LEVY = payroll.NMA_SPECIAL_LEVY,
                MEDCAN_DUES = payroll.MEDCAN_DUES,
                ARD = payroll.ARD,
                NARD = payroll.NARD,
                RENT = payroll.RENT,
                RENT_ARREARS = payroll.RENT_ARREARS,
                Deductions = payroll.Deductions,
                Gross_Pay = payroll.Gross_Pay,
                Net_Pay = payroll.Net_Pay,
                Bank_Name = payroll.Bank_Name,
                Bank_Branch = payroll.Bank_Branch,
                Account_Number = payroll.Account_Number,
                Sort_Code = payroll.Sort_Code
            };
        }

        [HttpGet]
        public IActionResult Activate() => View(new MultipleActivateViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessActivate(MultipleActivateViewModel model)
        {
            if (model == null) return View("Activate", new MultipleActivateViewModel());

            if (string.IsNullOrWhiteSpace(model.StaffIds))
            {
                ModelState.AddModelError("", "Enter one or more Staff IDs.");
                return View("Activate", model);
            }

            var staffIds = model.StaffIds
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            model.TotalEntered = staffIds.Count;

            int found = 0, activated = 0;
            var notFound = new List<string>();

            using var transaction = _context.Database.BeginTransaction();

            try
            {
                foreach (string staffId in staffIds)
                {
                    var deactivateRecord = _context.DeActivate.FirstOrDefault(x => x.StaffID == staffId);

                    if (deactivateRecord == null)
                    {
                        notFound.Add(staffId);
                        continue;
                    }

                    found++;
                    _context.Payroll.Add(CopyDeactivateToPayroll(deactivateRecord));
                    _context.DeActivate.Remove(deactivateRecord);
                    activated++;
                }

                _context.SaveChanges();
                transaction.Commit();

                model.TotalFound = found;
                model.TotalActivated = activated;
                model.TotalNotFound = notFound.Count;
                model.NotFoundStaffIds = notFound;

                TempData["Success"] = $"{activated} payroll record(s) successfully activated.";

                return View("Activate", model);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ModelState.AddModelError("", "An error occurred while activating payroll records.");
                ModelState.AddModelError("", ex.Message);

                return View("Activate", model);
            }
        }

        private Payroll CopyDeactivateToPayroll(DeActivate item)
        {
            return new Payroll
            {
                FileNo = item.FileNo,
                StaffID = item.StaffID,
                FullName = item.FullName,
                Staff_Group = item.Staff_Group,
                GradeLevel = item.GradeLevel,
                PaymentMonth = item.PaymentMonth,
                PaymentYear = item.PaymentYear,
                Consolidated = item.Consolidated,
                Allowance = item.Allowance,
                TeachingAllowance = item.TeachingAllowance,
                CallDutyAllowance = item.CallDutyAllowance,
                HazardAllowance = item.HazardAllowance,
                ShiftAllowance = item.ShiftAllowance,
                SpecialistAllowance = item.SpecialistAllowance,
                RuralPostingAllowance = item.RuralPostingAllowance,
                AccoutrementAllowance = item.AccoutrementAllowance,
                ConmessClinicalDutyAllowance = item.ConmessClinicalDutyAllowance,
                ConmessNonClinicalAllowance = item.ConmessNonClinicalAllowance,
                PharmacistConsultAllowance = item.PharmacistConsultAllowance,
                Arrears = item.Arrears,
                Refund = item.Refund,
                OverPaymentDeduction = item.OverPaymentDeduction,
                Pension = item.Pension,
                Tax = item.Tax,
                TAX_ARREARS = item.TAX_ARREARS,
                AMLSN = item.AMLSN,
                NMA_DUES = item.NMA_DUES,
                NMA_SPECIAL_LEVY = item.NMA_SPECIAL_LEVY,
                MEDCAN_DUES = item.MEDCAN_DUES,
                ARD = item.ARD,
                NARD = item.NARD,
                RENT = item.RENT,
                RENT_ARREARS = item.RENT_ARREARS,
                Deductions = item.Deductions,
                Gross_Pay = item.Gross_Pay,
                Net_Pay = item.Net_Pay,
                Bank_Name = item.Bank_Name,
                Bank_Branch = item.Bank_Branch,
                Account_Number = item.Account_Number,
                Sort_Code = item.Sort_Code
            };
        }

        private string GetBankCode(string bankName)
        {
            if (string.IsNullOrWhiteSpace(bankName)) return "";

            switch (bankName.Trim().ToUpper())
            {
                case "ACCESS BANK PLC": return "ACC";
                case "FIDELITY BANK PLC": return "FID";
                case "FIRST CITY MONUMENT BANK (FCMB)": return "FCMB";
                case "FIRST BANK OF NIGERIA LIMITED": return "FBN";
                case "GUARANTY TRUST BANK (GTBANK)": return "GTB";
                case "UNITED BANK FOR AFRICA (UBA)": return "UBA";
                case "ZENITH BANK PLC": return "ZEN";
                case "CITIBANK NIGERIA LIMITED": return "CITI";
                case "ECOBANK NIGERIA PLC": return "ECO";
                case "GLOBUS BANK LIMITED": return "GLOB";
                case "KEYSTONE BANK LIMITED": return "KEY";
                case "POLARIS BANK PLC": return "POL";
                case "STANBIC IBTC BANK PLC": return "SIBTC";
                case "STANDARD CHARTERED BANK NIGERIA": return "SCB";
                case "STERLING BANK PLC": return "STER";
                case "TITAN TRUST BANK LIMITED": return "TTB";
                case "UNION BANK OF NIGERIA PLC": return "UBN";
                case "UNITY BANK PLC": return "UNITY";
                case "WEMA BANK PLC": return "WEMA";
                case "JAIZ BANK PLC": return "JAIZ";
                case "PROVIDUS BANK LIMITED": return "PROV";
                case "SUNTRUST BANK": return "SUN";
                case "TAJ BANK": return "TAJ";
                default: return "";
            }
        }
    }
}