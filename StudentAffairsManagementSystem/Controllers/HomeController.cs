
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

            // الموظف ينتقل مباشرة إلى إدارة الطلاب
            if (User.IsInRole("Employee"))
            {
                return RedirectToAction(
                    "Index",
                    "Students"
                );
            }

            // الطالب ينتقل إلى لوحة الطالب
            if (User.IsInRole("Student"))
            {
                return RedirectToAction(
                    "Index",
                    "StudentDashboard"
                );
            }

            // أي مستخدم آخر غير مصرح له
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
