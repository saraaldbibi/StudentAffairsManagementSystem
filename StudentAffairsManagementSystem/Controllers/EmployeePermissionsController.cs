
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;
using StudentAffairsManagementSystem.Permissions;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class EmployeePermissionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Account> _userManager;

        public EmployeePermissionsController(
            ApplicationDbContext context,
            UserManager<Account> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // عرض الموظفين وصلاحيات حذف الطلاب
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .Include(e => e.Account)
                .AsNoTracking()
                .ToListAsync();

            var grantedAccountIds =
                await _context.AccountPermissions
                    .AsNoTracking()
                    .Where(ap =>
                        ap.Permission.Code ==
                            PermissionCodes.StudentsDelete &&
                        ap.IsActive &&
                        ap.RevokedAt == null)
                    .Select(ap => ap.AccountId)
                    .Distinct()
                    .ToListAsync();

            ViewBag.GrantedAccountIds = grantedAccountIds;

            return View(employees);
        }

        // ==========================================
        // منح صلاحية حذف الطلاب
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantDeleteStudents(
            long employeeId)
        {
            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null || employee.Account == null)
            {
                return NotFound();
            }

            // لا نمنح الصلاحية إلا لحساب يحمل دور موظف
            if (!await _userManager.IsInRoleAsync(
                    employee.Account, "Employee"))
            {
                return BadRequest(
                    "الحساب المحدد لا يحمل دور موظف.");
            }

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p =>
                    p.Code == PermissionCodes.StudentsDelete);

            if (permission == null)
            {
                TempData["ErrorMessage"] =
                    "صلاحية حذف الطلاب غير موجودة.";

                return RedirectToAction(nameof(Index));
            }

            long adminAccountId =
                long.Parse(_userManager.GetUserId(User)!);

            // هل لدى الموظف الصلاحية بالفعل؟
            bool alreadyGranted =
                await _context.AccountPermissions.AnyAsync(ap =>
                    ap.AccountId == employee.AccountId &&
                    ap.PermissionId == permission.Id &&
                    ap.IsActive &&
                    ap.RevokedAt == null);

            if (alreadyGranted)
            {
                TempData["ErrorMessage"] =
                    "الموظف يمتلك صلاحية حذف الطلاب بالفعل.";

                return RedirectToAction(nameof(Index));
            }

            // إنشاء سجل منح جديد للحفاظ على التاريخ
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

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "تم منح الموظف صلاحية حذف الطلاب بنجاح.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // سحب صلاحية حذف الطلاب
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeDeleteStudents(
            long employeeId)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
            {
                return NotFound();
            }

            var activeGrants =
                await _context.AccountPermissions
                    .Where(ap =>
                        ap.AccountId == employee.AccountId &&
                        ap.Permission.Code ==
                            PermissionCodes.StudentsDelete &&
                        ap.IsActive &&
                        ap.RevokedAt == null)
                    .ToListAsync();

            if (activeGrants.Count == 0)
            {
                TempData["ErrorMessage"] =
                    "الموظف لا يمتلك صلاحية حذف طلاب فعالة.";

                return RedirectToAction(nameof(Index));
            }

            foreach (var grant in activeGrants)
            {
                grant.IsActive = false;
                grant.RevokedAt = DateTime.UtcNow;
                grant.CanDelegate = false;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "تم سحب صلاحية حذف الطلاب من الموظف بنجاح.";

            return RedirectToAction(nameof(Index));
        }
    }
}
