
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<Account> _userManager;
        private readonly SignInManager<Account> _signInManager;

        private static readonly string[] AllowedRoles =
        {
            "Admin",
            "Employee",
            "Student"
        };

        public AccountController(
            UserManager<Account> userManager,
            SignInManager<Account> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // بيانات تسجيل الدخول
        public class LoginViewModel
        {
            [Required(
                ErrorMessage = "الرقم الجامعي أو الوظيفي مطلوب")]
            public string UserNumber { get; set; } =
                string.Empty;

            [Required(
                ErrorMessage = "كلمة المرور مطلوبة")]
            [DataType(DataType.Password)]
            public string Password { get; set; } =
                string.Empty;

            public bool RememberMe { get; set; }
        }

        // توجيه المستخدم حسب دوره
        private IActionResult RedirectByRole(string role)
        {
            if (role == "Admin")
            {
                return RedirectToAction(
                    "Index", "AdminDashboard");
            }

            // مؤقتاً لحين إنشاء لوحات الموظف والطالب
            return RedirectToAction(
                "Index", "Home");
        }

        // عرض صفحة تسجيل الدخول
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var currentUser =
                    await _userManager.GetUserAsync(User);

                if (currentUser != null)
                {
                    var roles =
                        await _userManager.GetRolesAsync(currentUser);

                    if (roles.Count == 1 &&
                        AllowedRoles.Contains(roles[0]) &&
                        currentUser.Role == roles[0])
                    {
                        return RedirectByRole(roles[0]);
                    }
                }

                // جلسة غير متطابقة مع بيانات الأدوار
                await _signInManager.SignOutAsync();
            }

            return View(new LoginViewModel());
        }

        // تنفيذ تسجيل الدخول
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string userNumber = model.UserNumber.Trim();

            if (string.IsNullOrWhiteSpace(userNumber))
            {
                ModelState.AddModelError(
                    nameof(LoginViewModel.UserNumber),
                    "الرقم الجامعي أو الوظيفي مطلوب");

                return View(model);
            }

            // البحث عن الحساب
            var user = await _userManager.FindByNameAsync(
                userNumber);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "الرقم أو كلمة المرور غير صحيحة");

                return View(model);
            }

            // فحص كلمة المرور مع تفعيل قفل الحساب
            var result = await _signInManager
                .CheckPasswordSignInAsync(
                    user,
                    model.Password,
                    lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "الحساب مقفل مؤقتاً بسبب " +
                    "محاولات دخول متكررة");

                return View(model);
            }

            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تسجيل الدخول غير مسموح لهذا الحساب");

                return View(model);
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "الرقم أو كلمة المرور غير صحيحة");

                return View(model);
            }

            // التأكد من وجود دور واحد صحيح
            var identityRoles =
                await _userManager.GetRolesAsync(user);

            if (identityRoles.Count != 1 ||
                !AllowedRoles.Contains(identityRoles[0]))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "صلاحيات هذا الحساب غير مهيأة بشكل صحيح. " +
                    "يرجى مراجعة مدير النظام.");

                return View(model);
            }

            // مطابقة الدور النصي مع Identity
            if (user.Role != identityRoles[0])
            {
                ModelState.AddModelError(
                    string.Empty,
                    "يوجد تعارض في صلاحيات الحساب. " +
                    "يرجى مراجعة مدير النظام.");

                return View(model);
            }

            // إنشاء جلسة تسجيل الدخول
            await _signInManager.SignInAsync(
                user,
                model.RememberMe);

            // الانتقال للوحة المناسبة حسب الدور
            return RedirectByRole(identityRoles[0]);
        }

        // تسجيل الخروج
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction(nameof(Login));
        }

        // رفض الوصول
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return StatusCode(
                403,
                "ليس لديك صلاحية للوصول إلى هذه الصفحة");
        }

        // إنشاء الأدوار الأساسية
        public static async Task EnsureRolesCreatedAsync(
            RoleManager<IdentityRole<long>> roleManager)
        {
            foreach (string roleName in AllowedRoles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(
                        new IdentityRole<long>(roleName));

                    if (!result.Succeeded)
                    {
                        string errors = string.Join(
                            "; ",
                            result.Errors.Select(
                                e => e.Description));

                        throw new InvalidOperationException(
                            $"تعذر إنشاء الدور {roleName}: {errors}");
                    }
                }
            }
        }

        // تعيين دور للحساب من كود إداري موثوق فقط
        // ليست Action ويمكن استدعاؤها من الكود فقط
        public static async Task<IdentityResult> AssignRoleAsync(
            UserManager<Account> userManager,
            Account account,
            string roleName)
        {
            if (!AllowedRoles.Contains(roleName))
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Description = "الدور المطلوب غير صالح"
                    });
            }

            // إضافة الدور المطلوب
            if (!await userManager.IsInRoleAsync(
                account, roleName))
            {
                var addResult =
                    await userManager.AddToRoleAsync(
                        account, roleName);

                if (!addResult.Succeeded)
                    return addResult;
            }

            // إزالة الأدوار الأخرى
            var currentRoles =
                await userManager.GetRolesAsync(account);

            var rolesToRemove = currentRoles
                .Where(r => r != roleName)
                .ToArray();

            if (rolesToRemove.Length > 0)
            {
                var removeResult =
                    await userManager.RemoveFromRolesAsync(
                        account, rolesToRemove);

                if (!removeResult.Succeeded)
                    return removeResult;
            }

            // تحديث الدور النصي
            account.Role = roleName;

            return await userManager.UpdateAsync(account);
        }
    }
}
