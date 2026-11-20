
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class EmployeePermissionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Account> _userManager;

        private const string StudentRegistrationPermission =
            "Students.RegisterCourses";

        // صلاحيات الموظفين الموجودة سابقًا
        private static readonly Dictionary<string, string>
            ManagedPermissions = new()
            {
                { "Students.Delete", "حذف طالب" },
                { "Students.Create", "إنشاء حسابات الطلاب" },
                { "Registrations.Manage", "تسجيل المقررات الدراسية" }
            };

        public EmployeePermissionsController(
            ApplicationDbContext context,
            UserManager<Account> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // التأكد من وجود تعريف الصلاحية
        // ==========================================
        private async Task<Permission> EnsurePermissionAsync(
            string code,
            string name)
        {
            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Code == code);

            if (permission != null)
                return permission;

            permission = new Permission
            {
                Code = code,
                Name = name
            };

            _context.Permissions.Add(permission);
            await _context.SaveChangesAsync();

            return permission;
        }

        // ==========================================
        // عرض صفحة إدارة الصلاحيات
        // GET: /EmployeePermissions
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .Include(e => e.Account)
                .AsNoTracking()
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToListAsync();

            // التأكد من تعريف صلاحيات الموظفين
            var existingPermissions = await _context.Permissions
                .AsNoTracking()
                .Where(p =>
                    ManagedPermissions.Keys.Contains(p.Code))
                .Select(p => p.Code)
                .ToListAsync();

            var missingPermissions = ManagedPermissions
                .Where(p =>
                    !existingPermissions.Contains(p.Key))
                .Select(p => new Permission
                {
                    Code = p.Key,
                    Name = p.Value
                })
                .ToList();

            if (missingPermissions.Count > 0)
            {
                _context.Permissions.AddRange(missingPermissions);
                await _context.SaveChangesAsync();
            }

            // تعريف صلاحية الطلاب
            await EnsurePermissionAsync(
                StudentRegistrationPermission,
                "تسجيل المقررات الدراسية للطلاب");

            // صلاحيات الموظفين الفعالة
            var accountPermissions =
                await _context.AccountPermissions
                    .AsNoTracking()
                    .Where(ap =>
                        ap.IsActive &&
                        ap.RevokedAt == null &&
                        ManagedPermissions.Keys.Contains(
                            ap.Permission.Code))
                    .Select(ap => new
                    {
                        ap.AccountId,
                        PermissionCode = ap.Permission.Code
                    })
                    .Distinct()
                    .ToListAsync();

            ViewBag.EmployeePermissions = accountPermissions
                .GroupBy(ap => ap.AccountId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x =>
                        x.PermissionCode).ToList());

            ViewBag.ManagedPermissions = ManagedPermissions;

            ViewBag.TotalEmployees = employees.Count;

            ViewBag.EmployeesWithPermissions =
                employees.Count(e =>
                    accountPermissions.Any(ap =>
                        ap.AccountId == e.AccountId));

            // ==========================================
            // إحصائيات صلاحية تسجيل الطلاب
            // ==========================================
            var activeStudents = await _context.Students
                .AsNoTracking()
                .Where(s =>
                    !s.IsDeleted &&
                    s.AccountId != null)
                .Select(s => new
                {
                    s.Id,
                    s.AccountId
                })
                .ToListAsync();

            var studentAccountIds = activeStudents
                .Select(s => s.AccountId)
                .Distinct()
                .ToList();

            var permittedStudentAccountIds =
                await _context.AccountPermissions
                    .AsNoTracking()
                    .Where(ap =>
                        studentAccountIds.Contains(ap.AccountId) &&
                        ap.Permission.Code ==
                            StudentRegistrationPermission &&
                        ap.IsActive &&
                        ap.RevokedAt == null)
                    .Select(ap => ap.AccountId)
                    .Distinct()
                    .ToListAsync();

            ViewBag.TotalStudents =
                activeStudents.Count;

            ViewBag.StudentsWithRegistrationPermission =
                activeStudents.Count(s =>
                    permittedStudentAccountIds.Contains(
                        s.AccountId));

            ViewBag.StudentsWithoutRegistrationPermission =
                activeStudents.Count -
                (int)ViewBag.StudentsWithRegistrationPermission;

            return View(employees);
        }

        // ==========================================
        // منح صلاحية محددة لموظف
        // POST: /EmployeePermissions/Grant
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grant(
            long employeeId,
            string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(permissionCode) ||
                !ManagedPermissions.ContainsKey(permissionCode))
            {
                TempData["ErrorMessage"] =
                    "الصلاحية المحددة غير معتمدة.";

                return RedirectToAction(nameof(Index));
            }

            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e =>
                    e.Id == employeeId &&
                    !e.IsDeleted);

            if (employee == null || employee.Account == null)
            {
                TempData["ErrorMessage"] =
                    "الموظف المحدد غير موجود أو غير فعال.";

                return RedirectToAction(nameof(Index));
            }

            if (!await _userManager.IsInRoleAsync(
                employee.Account, "Employee"))
            {
                TempData["ErrorMessage"] =
                    "لا يمكن منح الصلاحية لحساب لا يحمل دور موظف.";

                return RedirectToAction(nameof(Index));
            }

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p =>
                    p.Code == permissionCode);

            if (permission == null)
            {
                TempData["ErrorMessage"] =
                    "تعذر العثور على تعريف الصلاحية.";

                return RedirectToAction(nameof(Index));
            }

            bool alreadyGranted =
                await _context.AccountPermissions
                    .AnyAsync(ap =>
                        ap.AccountId == employee.AccountId &&
                        ap.PermissionId == permission.Id &&
                        ap.IsActive &&
                        ap.RevokedAt == null);

            if (alreadyGranted)
            {
                TempData["ErrorMessage"] =
                    "الموظف يمتلك هذه الصلاحية بالفعل.";

                return RedirectToAction(nameof(Index));
            }

            string? currentAdminId =
                _userManager.GetUserId(User);

            if (!long.TryParse(
                currentAdminId,
                out long adminAccountId))
            {
                return Forbid();
            }

            var accountPermission = new AccountPermission
            {
                AccountId = employee.AccountId,
                PermissionId = permission.Id,
                GrantedByAccountId = adminAccountId,
                CanDelegate = false,
                IsActive = true,
                GrantedAt = DateTime.UtcNow,
                RevokedAt = null,
                ParentGrantId = null
            };

            _context.AccountPermissions.Add(accountPermission);

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"تم منح صلاحية «{ManagedPermissions[permissionCode]}» للموظف بنجاح.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "تعذر منح الصلاحية بسبب تعارض في البيانات.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // سحب صلاحية محددة من موظف
        // POST: /EmployeePermissions/Revoke
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(
            long employeeId,
            string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(permissionCode) ||
                !ManagedPermissions.ContainsKey(permissionCode))
            {
                TempData["ErrorMessage"] =
                    "الصلاحية المحددة غير معتمدة.";

                return RedirectToAction(nameof(Index));
            }

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.Id == employeeId &&
                    !e.IsDeleted);

            if (employee == null)
            {
                TempData["ErrorMessage"] =
                    "الموظف المحدد غير موجود أو غير فعال.";

                return RedirectToAction(nameof(Index));
            }

            var activeGrants =
                await _context.AccountPermissions
                    .Where(ap =>
                        ap.AccountId == employee.AccountId &&
                        ap.Permission.Code == permissionCode &&
                        ap.IsActive &&
                        ap.RevokedAt == null)
                    .ToListAsync();

            if (activeGrants.Count == 0)
            {
                TempData["ErrorMessage"] =
                    "الموظف لا يمتلك هذه الصلاحية حاليًا.";

                return RedirectToAction(nameof(Index));
            }

            DateTime revokedAt = DateTime.UtcNow;

            foreach (var grant in activeGrants)
            {
                grant.IsActive = false;
                grant.RevokedAt = revokedAt;
                grant.CanDelegate = false;
            }

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"تم سحب صلاحية «{ManagedPermissions[permissionCode]}» من الموظف بنجاح.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "تعذر سحب الصلاحية بسبب تعارض في البيانات.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // منح صلاحية التسجيل لجميع الطلاب
        // POST: /EmployeePermissions/GrantAllStudents
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantAllStudents()
        {
            string? currentAdminId =
                _userManager.GetUserId(User);

            if (!long.TryParse(
                currentAdminId,
                out long adminAccountId))
            {
                return Forbid();
            }

            try
            {
                await using var transaction =
                    await _context.Database
                        .BeginTransactionAsync(
                            IsolationLevel.Serializable);

                var permission = await EnsurePermissionAsync(
                    StudentRegistrationPermission,
                    "تسجيل المقررات الدراسية للطلاب");

                // الطلاب غير المحذوفين المرتبطون بحسابات
                var students = await _context.Students
                    .Include(s => s.Account)
                    .Where(s =>
                        !s.IsDeleted &&
                        s.AccountId != null &&
                        s.Account != null)
                    .ToListAsync();

                // نتحقق من الدور الفعلي لكل حساب
                var eligibleAccountIds = new List<long>();

                foreach (var student in students)
                {
                    if (student.Account == null)
                        continue;

                    if (await _userManager.IsInRoleAsync(
                        student.Account, "Student"))
                    {
                        eligibleAccountIds.Add(
                            student.AccountId);
                    }
                }

                eligibleAccountIds =
                    eligibleAccountIds.Distinct().ToList();

                var existingGrants =
                    await _context.AccountPermissions
                        .Where(ap =>
                            eligibleAccountIds.Contains(ap.AccountId) &&
                            ap.PermissionId == permission.Id)
                        .ToListAsync();

                int grantedCount = 0;
                DateTime grantedAt = DateTime.UtcNow;

                foreach (long accountId in eligibleAccountIds)
                {
                    // لا نكرر المنح إذا الصلاحية فعالة
                    bool alreadyActive = existingGrants.Any(ap =>
                        ap.AccountId == accountId &&
                        ap.IsActive &&
                        ap.RevokedAt == null);

                    if (alreadyActive)
                        continue;

                    // نعيد استخدام السجل الملغى إن وجد،
                    // لتجنب التعارض مع أي فهرس فريد
                    // على الحساب والصلاحية.
                    var previousGrant = existingGrants
                        .Where(ap =>
                            ap.AccountId == accountId &&
                            !ap.IsActive)
                        .OrderByDescending(ap => ap.GrantedAt)
                        .FirstOrDefault();

                    if (previousGrant != null)
                    {
                        previousGrant.IsActive = true;
                        previousGrant.RevokedAt = null;
                        previousGrant.GrantedAt = grantedAt;
                        previousGrant.GrantedByAccountId =
                            adminAccountId;
                        previousGrant.CanDelegate = false;
                        previousGrant.ParentGrantId = null;
                    }
                    else
                    {
                        _context.AccountPermissions.Add(
                            new AccountPermission
                            {
                                AccountId = accountId,
                                PermissionId = permission.Id,
                                GrantedByAccountId =
                                    adminAccountId,
                                CanDelegate = false,
                                IsActive = true,
                                GrantedAt = grantedAt,
                                RevokedAt = null,
                                ParentGrantId = null
                            });
                    }

                    grantedCount++;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    grantedCount > 0
                        ? $"تم منح صلاحية تسجيل المقررات إلى {grantedCount} حساب طالب بنجاح."
                        : "جميع حسابات الطلاب المؤهلة تمتلك صلاحية تسجيل المقررات بالفعل.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "تعذر منح صلاحية التسجيل لجميع الطلاب بسبب تعارض في البيانات.";
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is Microsoft.Data.SqlClient.SqlException)
            {
                TempData["ErrorMessage"] =
                    "تعذر إتمام منح الصلاحيات حاليًا.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // سحب صلاحية التسجيل من جميع الطلاب
        // POST: /EmployeePermissions/RevokeAllStudents
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeAllStudents()
        {
            try
            {
                await using var transaction =
                    await _context.Database
                        .BeginTransactionAsync(
                            IsolationLevel.Serializable);

                var activeStudentAccountIds =
                    await _context.Students
                        .AsNoTracking()
                        .Where(s =>
                            !s.IsDeleted &&
                            s.AccountId != null)
                        .Select(s => s.AccountId)
                        .Distinct()
                        .ToListAsync();

                var activeGrants =
                    await _context.AccountPermissions
                        .Where(ap =>
                            activeStudentAccountIds.Contains(
                                ap.AccountId) &&
                            ap.Permission.Code ==
                                StudentRegistrationPermission &&
                            ap.IsActive &&
                            ap.RevokedAt == null)
                        .ToListAsync();

                DateTime revokedAt = DateTime.UtcNow;

                foreach (var grant in activeGrants)
                {
                    grant.IsActive = false;
                    grant.RevokedAt = revokedAt;
                    grant.CanDelegate = false;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    activeGrants.Count > 0
                        ? $"تم سحب صلاحية تسجيل المقررات من {activeGrants.Select(g => g.AccountId).Distinct().Count()} حساب طالب بنجاح."
                        : "لا توجد صلاحيات تسجيل فعالة لسحبها من الطلاب.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "تعذر سحب صلاحيات التسجيل بسبب تعارض في البيانات.";
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is Microsoft.Data.SqlClient.SqlException)
            {
                TempData["ErrorMessage"] =
                    "تعذر إتمام سحب الصلاحيات حاليًا.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // توافق مع الأزرار القديمة
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> GrantDeleteStudents(
            long employeeId)
        {
            return Grant(employeeId, "Students.Delete");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> RevokeDeleteStudents(
            long employeeId)
        {
            return Revoke(employeeId, "Students.Delete");
        }
    }
}
