
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentAffairsManagementSystem.Models;
using System.Diagnostics;

namespace StudentAffairsManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        // الصفحة الرئيسية للنظام
        public IActionResult Index()
        {
            // إذا المستخدم غير مسجل الدخول
            // نوجهه لصفحة تسجيل الدخول
            if (User.Identity?.IsAuthenticated != true)
            {
                return Challenge();
            }

            // مدير النظام ينتقل إلى لوحة التحكم
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction(
                    "Index",
                    "AdminDashboard"
                );
            }

            // مؤقتاً: الموظف ينتقل إلى صفحة التسجيلات
            if (User.IsInRole("Employee"))
            {
                return RedirectToAction(
                    "Index",
                    "Registrations"
                );
            }

            // أي مستخدم آخر ليس لديه لوحة مخصصة بعد
            return Forbid();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId =
                    Activity.Current?.Id ??
                    HttpContext.TraceIdentifier
            });
        }
    }
}
