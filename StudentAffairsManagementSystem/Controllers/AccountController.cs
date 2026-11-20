
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Account> _signInManager;
        private readonly UserManager<Account> _userManager;

        public AccountController(
            SignInManager<Account> signInManager,
            UserManager<Account> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // نموذج تسجيل الدخول
        public class LoginViewModel
        {
            [Required(
                ErrorMessage = "الرقم الجامعي أو الوظيفي مطلوب")]
            public string UserNumber { get; set; } = string.Empty;

            [Required(
                ErrorMessage = "كلمة المرور مطلوبة")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; }
        }

        // عرض صفحة تسجيل الدخول
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectByRole();
            }

            ViewData["ReturnUrl"] = returnUrl;

            return View(new LoginViewModel());
        }

        // تنفيذ تسجيل الدخول
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string userNumber = model.UserNumber.Trim();

            // البحث عن الحساب في Identity
            var user = await _userManager.FindByNameAsync(
                userNumber);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "الرقم أو كلمة المرور غير صحيحة.");

                return View(model);
            }

            // التحقق من كلمة المرور
            var result = await _signInManager.PasswordSignInAsync(
                user,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "الحساب مقفل. يرجى مراجعة إدارة النظام.");

                return View(model);
            }

            if (result.RequiresTwoFactor)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "يتطلب الحساب تحققًا إضافيًا.");

                return View(model);
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "الرقم أو كلمة المرور غير صحيحة.");

                return View(model);
            }

            // مدير النظام
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction(
                    "Index",
                    "AdminDashboard");
            }

            // الموظف: الانتقال مباشرة إلى إدارة الطلاب
            if (await _userManager.IsInRoleAsync(user, "Employee"))
            {
                return RedirectToAction(
                    "Index",
                    "Students");
            }

            // الطالب: لوحة الطالب
            if (await _userManager.IsInRoleAsync(user, "Student"))
            {
                return RedirectToAction(
                    "Index",
                    "StudentDashboard");
            }

            // منع الحسابات التي لا تمتلك دورًا معروفًا
            await _signInManager.SignOutAsync();

            ModelState.AddModelError(
                string.Empty,
                "الحساب لا يمتلك صلاحية الدخول إلى النظام.");

            return View(model);
        }

        // تسجيل الخروج
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction(
                "Login",
                "Account");
        }

        // توجيه المستخدم المسجل دخوله
        private IActionResult RedirectByRole()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction(
                    "Index",
                    "AdminDashboard");
            }

            if (User.IsInRole("Employee"))
            {
                return RedirectToAction(
                    "Index",
                    "Students");
            }

            if (User.IsInRole("Student"))
            {
                return RedirectToAction(
                    "Index",
                    "StudentDashboard");
            }

            return Forbid();
        }

        // إنشاء الأدوار الأساسية للنظام
        // متوافقة مع الاستدعاء الموجود في Program.cs
        public static async Task EnsureRolesCreatedAsync(
            RoleManager<IdentityRole<long>> roleManager)
        {
            string[] roles =
            {
                "Admin",
                "Employee",
                "Student"
            };

            foreach (string role in roles)
            {
                bool roleExists =
                    await roleManager.RoleExistsAsync(role);

                if (!roleExists)
                {
                    var result =
                        await roleManager.CreateAsync(
                            new IdentityRole<long>(role));

                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"تعذر إنشاء الدور: {role}");
                    }
                }
            }
        }
    }
}
