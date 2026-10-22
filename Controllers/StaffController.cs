using Microsoft.AspNetCore.Mvc;
using PayrollSystem.Data;
using PayrollSystem.Models;
using System.Linq;

namespace PayrollSystem.Controllers
{
    public class StaffController : Controller
    {
        private readonly AppDbContext _context;

        public StaffController(AppDbContext context)
        {
            _context = context;
        }

        // GET
        public IActionResult Create()
        {
            return View();
        }

        // POST
        [HttpPost]
        public IActionResult Create(Payroll payroll)
        {
            // CHECK DUPLICATE StaffID OR FileNo
            bool recordExists = _context.Payroll
                .Any(x => x.StaffID == payroll.StaffID || x.FileNo == payroll.FileNo);

            if (recordExists)
            {
                ViewBag.ErrorMessage = "Record already exist";
                return View(payroll);
            }

            // SAVE
            _context.Payroll.Add(payroll);
            _context.SaveChanges();

            ViewBag.Message = "Information submitted successfully";

            ModelState.Clear();

            return View();
        }
    }
}