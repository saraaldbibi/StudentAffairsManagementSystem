
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
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

        // الأدوار المسموح بها في النظام
        private static readonly string[] AllowedRoles =
        {
            "Admin",
            "Employee",
            "Student"
        };

        // ==========================================
        // Constructor
        // ==========================================
        public AccountController(
            UserManager<Account> userManager,
            SignInManager<Account> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // ==========================================
        // Login ViewModel
        // ==========================================
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

        // ==========================================
        // توجيه المستخدم حسب دوره
        // ==========================================
        private IActionResult RedirectByRole(string role)
        {
            // المدير: دون تغيير
            if (role == "Admin")
            {
                return RedirectToAction(
                    "Index",
                    "AdminDashboard");
            }

            // الموظف: فتح إدارة التسجيلات تلقائيًا
            if (role == "Employee")
            {
                return RedirectToAction(
                    "Manage",
                    "Registrations");
            }

            // الطالب: دون تغيير
            if (role == "Student")
            {
                return RedirectToAction(
                    "Index",
                    "StudentDashboard");
            }

            // أي دور غير معروف
            return Forbid();
        }

        // ==========================================
        // GET: Account/Login
        // ==========================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login()
        {
            // إذا كان المستخدم مسجل الدخول مسبقًا
            if (User.Identity?.IsAuthenticated == true)
            {
                var currentUser =
                    await _userManager.GetUserAsync(User);

                if (currentUser != null)
                {
                    var roles =
                        await _userManager.GetRolesAsync(
                            currentUser);

                    // التأكد من تطابق الدور
                    if (roles.Count == 1 &&
                        AllowedRoles.Contains(roles[0]) &&
                        currentUser.Role == roles[0])
                    {
                        return RedirectByRole(roles[0]);
                    }
                }

                // تسجيل الخروج إذا كانت بيانات
                // الأدوار غير متطابقة
                await _signInManager.SignOutAsync();
            }

            return View(new LoginViewModel());
        }

        // ==========================================
        // POST: Account/Login
        // ==========================================
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // إزالة الفراغات من الرقم
            string userNumber = model.UserNumber.Trim();

            if (string.IsNullOrWhiteSpace(userNumber))
            {
                ModelState.AddModelError(
                    nameof(LoginViewModel.UserNumber),
                    "الرقم الجامعي أو الوظيفي مطلوب");

                return View(model);
            }

            // ======================================
            // البحث عن الحساب
            // ======================================
            var user =
                await _userManager.FindByNameAsync(
                    userNumber);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "الرقم أو كلمة المرور غير صحيحة");

                return View(model);
            }

            // ======================================
            // التحقق من كلمة المرور
            // ======================================
            var result = await _signInManager
                .CheckPasswordSignInAsync(
                    user,
                    model.Password,
                    lockoutOnFailure: true);

            // الحساب مقفل
            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "الحساب مقفل مؤقتًا بسبب " +
                    "محاولات دخول متكررة");

                return View(model);
            }

            // تسجيل الدخول غير مسموح
            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تسجيل الدخول غير مسموح لهذا الحساب");

                return View(model);
            }

            // كلمة المرور غير صحيحة
            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "الرقم أو كلمة المرور غير صحيحة");

                return View(model);
            }

            // ======================================
            // التحقق من الأدوار
            // ======================================
            var identityRoles =
                await _userManager.GetRolesAsync(user);

            // يجب أن يمتلك الحساب دورًا واحدًا فقط
            if (identityRoles.Count != 1 ||
                !AllowedRoles.Contains(identityRoles[0]))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "صلاحيات هذا الحساب غير مهيأة بشكل صحيح. " +
                    "يرجى مراجعة مدير النظام.");

                return View(model);
            }

            // التأكد من تطابق الدور النصي
            // مع الدور الموجود في Identity
            if (user.Role != identityRoles[0])
            {
                ModelState.AddModelError(
                    string.Empty,
                    "يوجد تعارض في صلاحيات الحساب. " +
                    "يرجى مراجعة مدير النظام.");

                return View(model);
            }

            // ======================================
            // إنشاء جلسة تسجيل الدخول
            // ======================================
            await _signInManager.SignInAsync(
                user,
                model.RememberMe);

            // ======================================
            // التوجيه حسب الدور
            // ======================================
            return RedirectByRole(identityRoles[0]);
        }

        // ==========================================
        // POST: Account/Logout
        // ==========================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction(nameof(Login));
        }

        // ==========================================
        // GET: Account/AccessDenied
        // ==========================================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return StatusCode(
                403,
                "ليس لديك صلاحية للوصول إلى هذه الصفحة");
        }

        // ==========================================
        // إنشاء الأدوار الأساسية
        // ==========================================
        public static async Task EnsureRolesCreatedAsync(
            RoleManager<IdentityRole<long>> roleManager)
        {
            foreach (string roleName in AllowedRoles)
            {
                if (!await roleManager.RoleExistsAsync(
                    roleName))
                {
                    var result =
                        await roleManager.CreateAsync(
                            new IdentityRole<long>(
                                roleName));

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

        // ==========================================
        // تعيين دور للحساب
        // تستدعى من كود إداري موثوق فقط
        // ==========================================
        public static async Task<IdentityResult> AssignRoleAsync(
            UserManager<Account> userManager,
            Account account,
            string roleName)
        {
            // التحقق من الدور المطلوب
            if (!AllowedRoles.Contains(roleName))
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Description = "الدور المطلوب غير صالح"
                    });
            }

            // ======================================
            // إضافة الدور الجديد
            // ======================================
            if (!await userManager.IsInRoleAsync(
                account,
                roleName))
            {
                var addResult =
                    await userManager.AddToRoleAsync(
                        account,
                        roleName);

                if (!addResult.Succeeded)
                {
                    return addResult;
                }
            }

            // ======================================
            // إزالة الأدوار الأخرى
            // ======================================
            var currentRoles =
                await userManager.GetRolesAsync(account);

            var rolesToRemove = currentRoles
                .Where(r => r != roleName)
                .ToArray();

            if (rolesToRemove.Length > 0)
            {
                var removeResult =
                    await userManager.RemoveFromRolesAsync(
                        account,
                        rolesToRemove);

                if (!removeResult.Succeeded)
                {
                    return removeResult;
                }
            }

            // ======================================
            // تحديث الدور النصي
            // ======================================
            account.Role = roleName;

            return await userManager.UpdateAsync(account);
        }
    }
}
