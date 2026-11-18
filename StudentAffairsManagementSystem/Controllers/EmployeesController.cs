
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;
using StudentAffairsManagementSystem.ViewModels;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Account> _userManager;

        public EmployeesController(
            ApplicationDbContext context,
            UserManager<Account> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // عرض الموظفين غير المحذوفين
        // ==========================================
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.Id)
                .ToListAsync();

            return View(employees);
        }

        // ==========================================
        // تفاصيل الموظف
        // ==========================================
        public async Task<IActionResult> Details(long id)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.Id == id && !e.IsDeleted);

            if (employee == null)
                return NotFound();

            return View(employee);
        }

        // ==========================================
        // صفحة إضافة موظف
        // ==========================================
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateEmployeeViewModel());
        }

        // ==========================================
        // إنشاء الموظف وحساب Identity
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateEmployeeViewModel model)
        {
            model.EmployeeNumber =
                model.EmployeeNumber?.Trim() ?? "";

            bool numberExists = await _context.Employees
                .AnyAsync(e =>
                    e.EmployeeNumber == model.EmployeeNumber);

            if (numberExists)
            {
                ModelState.AddModelError(
                    nameof(model.EmployeeNumber),
                    "الرقم الوظيفي مستخدم مسبقاً");
            }

            var existingAccount =
                await _userManager.FindByNameAsync(
                    model.EmployeeNumber);

            if (existingAccount != null)
            {
                ModelState.AddModelError(
                    nameof(model.EmployeeNumber),
                    "يوجد حساب بهذا الرقم الوظيفي");
            }

            if (!ModelState.IsValid)
                return View(model);

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var account = new Account
            {
                UserName = model.EmployeeNumber,
                PhoneNumber = model.Phone,
                Role = "Employee"
            };

            var createResult =
                await _userManager.CreateAsync(
                    account,
                    model.Password);

            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync();

                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return View(model);
            }

            var roleResult =
                await _userManager.AddToRoleAsync(
                    account,
                    "Employee");

            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();

                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return View(model);
            }

            var employee = new Employee
            {
                EmployeeNumber = model.EmployeeNumber,
                FirstName = model.FirstName,
                FatherName = model.FatherName,
                LastName = model.LastName,
                Phone = model.Phone,
                Address = model.Address,
                AccountId = account.Id,
                IsDeleted = false,
                DeletedAt = null
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return RedirectToAction(
                nameof(Details),
                new { id = employee.Id });
        }

        // ==========================================
        // صفحة تعديل الموظف
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e =>
                    e.Id == id && !e.IsDeleted);

            if (employee == null)
                return NotFound();

            return View(employee);
        }

        // ==========================================
        // حفظ تعديل الموظف
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind("Id,FirstName,FatherName,LastName,Phone,Address")]
            Employee model)
        {
            if (id != model.Id)
                return NotFound();

            ModelState.Remove(nameof(Employee.Account));
            ModelState.Remove(nameof(Employee.EmployeeNumber));

            if (!ModelState.IsValid)
                return View(model);

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e =>
                    e.Id == id && !e.IsDeleted);

            if (employee == null)
                return NotFound();

            employee.FirstName = model.FirstName;
            employee.FatherName = model.FatherName;
            employee.LastName = model.LastName;
            employee.Phone = model.Phone;
            employee.Address = model.Address;

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var account = await _userManager.FindByIdAsync(
                employee.AccountId.ToString());

            if (account != null)
            {
                account.PhoneNumber = model.Phone;

                var result =
                    await _userManager.UpdateAsync(account);

                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            error.Description);
                    }

                    return View(model);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(
                nameof(Details),
                new { id = employee.Id });
        }

        // ==========================================
        // صفحة تأكيد الحذف الآمن
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.Id == id && !e.IsDeleted);

            if (employee == null)
                return NotFound();

            return View(employee);
        }

        // ==========================================
        // تنفيذ الحذف الآمن للموظف
        // ==========================================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e =>
                    e.Id == id && !e.IsDeleted);

            if (employee == null)
                return NotFound();

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var account = await _userManager.FindByIdAsync(
                employee.AccountId.ToString());

            if (account == null)
            {
                TempData["Error"] =
                    "تعذر العثور على حساب الموظف المرتبط";

                return RedirectToAction(nameof(Index));
            }

            DateTime deletedAt = DateTime.UtcNow;

            // حذف آمن لسجل الموظف
            employee.IsDeleted = true;
            employee.DeletedAt = deletedAt;

            // تعطيل حساب الدخول
            account.LockoutEnabled = true;
            account.LockoutEnd = DateTimeOffset.MaxValue;

            // إبطال جلسات الدخول الحالية
            var securityResult =
                await _userManager.UpdateSecurityStampAsync(account);

            if (!securityResult.Succeeded)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "تعذر تعطيل حساب الموظف";

                return RedirectToAction(nameof(Index));
            }

            // تعطيل صلاحيات الموظف والمنح التابعة لها
            var allGrants = await _context.AccountPermissions
                .ToListAsync();

            var idsToRevoke = new HashSet<long>(
                allGrants
                    .Where(ap =>
                        ap.AccountId == employee.AccountId ||
                        ap.GrantedByAccountId == employee.AccountId)
                    .Select(ap => ap.Id));

            bool added;

            do
            {
                added = false;

                foreach (var grant in allGrants)
                {
                    if (grant.ParentGrantId.HasValue &&
                        idsToRevoke.Contains(
                            grant.ParentGrantId.Value) &&
                        idsToRevoke.Add(grant.Id))
                    {
                        added = true;
                    }
                }
            }
            while (added);

            foreach (var grant in allGrants)
            {
                if (idsToRevoke.Contains(grant.Id) &&
                    grant.IsActive &&
                    grant.RevokedAt == null)
                {
                    grant.IsActive = false;
                    grant.CanDelegate = false;
                    grant.RevokedAt = deletedAt;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] =
                "تم حذف الموظف بأمان وتعطيل حسابه وصلاحياته";

            return RedirectToAction(nameof(Index));
        }
    }
}
