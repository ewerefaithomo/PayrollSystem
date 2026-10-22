using Microsoft.AspNetCore.Mvc;

namespace PayrollSystem.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // Redirects any Home/Dashboard call seamlessly to Account/Dashboard
        public IActionResult Dashboard()
        {
            return RedirectToAction("Dashboard", "Account");
        }
    }
}