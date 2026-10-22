using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PayrollSystem.Data;
using System.Linq;

namespace PayrollSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private const string REQUIRED_ACCESS_CODE = "123456";

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Login() => View();

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var user = _context.Users
                .FirstOrDefault(u => u.Username == username && u.Password == password);

            if (user != null)
            {
                HttpContext.Session.SetString("Username", user.Username);
                return RedirectToAction("Dashboard");
            }

            ViewBag.Message = "Invalid username and password";
            return View();
        }

        // =========================================================
        // LOGOUT ROUTE
        // =========================================================
        public IActionResult Logout()
        {
            // Clear session data upon logging out
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // =========================================================
        // ACCESS CODE ROUTING (GET & POST)
        // =========================================================
        public IActionResult VerifyAccessCode() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyAccessCode(string accessCode)
        {
            if (accessCode == REQUIRED_ACCESS_CODE)
            {
                HttpContext.Session.SetString("AccessGranted", "true");
                return RedirectToAction("Signup");
            }

            ViewBag.Message = "Wrong Access Code entered.";
            return View();
        }

        // =========================================================
        // SIGNUP ROUTING (PROTECTED BY SESSION LAYER)
        // =========================================================
        public IActionResult Signup()
        {
            if (HttpContext.Session.GetString("AccessGranted") != "true")
            {
                return RedirectToAction("VerifyAccessCode");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Signup(User user)
        {
            if (HttpContext.Session.GetString("AccessGranted") != "true")
            {
                return RedirectToAction("VerifyAccessCode");
            }

            if (_context.Users.Any(u => u.Username == user.Username))
            {
                ViewBag.Message = "User account already exists";
                return View();
            }

            _context.Users.Add(user);
            _context.SaveChanges();

            HttpContext.Session.Remove("AccessGranted");

            return RedirectToAction("Login");
        }

        public IActionResult Dashboard()
        {
            ViewBag.Username = HttpContext.Session.GetString("Username");
            return View();
        }
    }
}