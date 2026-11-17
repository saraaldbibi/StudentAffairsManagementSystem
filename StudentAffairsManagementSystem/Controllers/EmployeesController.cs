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

        // عرض جميع الموظفين
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .OrderBy(e => e.Id)
                .ToListAsync();

            return View(employees);
        }

        // عرض تفاصيل موظف
        public async Task<IActionResult> Details(long id)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            return View(employee);
        }

        // فتح صفحة إضافة موظف
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateEmployeeViewModel());
        }

        // إنشاء موظف وحسابه
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateEmployeeViewModel model)
        {
            model.EmployeeNumber =
                model.EmployeeNumber?.Trim() ?? "";

            // منع تكرار الرقم الوظيفي
            bool numberExists = await _context.Employees
                .AnyAsync(e =>
                    e.EmployeeNumber == model.EmployeeNumber);

            if (numberExists)
            {
                ModelState.AddModelError(
                    nameof(model.EmployeeNumber),
                    "الرقم الوظيفي مستخدم مسبقاً");
            }

            // منع وجود حساب بنفس الرقم
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

            // حفظ الحساب والدور والموظف معاً
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var account = new Account
            {
                UserName = model.EmployeeNumber,
                PhoneNumber = model.Phone,
                Role = "Employee"
            };

            // إنشاء حساب الموظف
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

            // إضافة دور Employee الحقيقي
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

            // إنشاء سجل الموظف
            var employee = new Employee
            {
                EmployeeNumber = model.EmployeeNumber,
                FirstName = model.FirstName,
                FatherName = model.FatherName,
                LastName = model.LastName,
                Phone = model.Phone,
                Address = model.Address,
                AccountId = account.Id
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            // تثبيت الحساب والدور والموظف
            await transaction.CommitAsync();

            return RedirectToAction(
                nameof(Details),
                new { id = employee.Id });
        }

        // فتح صفحة تعديل الموظف
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            return View(employee);
        }

        // حفظ تعديلات الموظف
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind("Id,FirstName,FatherName,LastName,Phone,Address")]
            Employee model)
        {
            if (id != model.Id)
                return NotFound();

            // تجاهل الحقول غير الموجودة بالنموذج
            ModelState.Remove(nameof(Employee.Account));
            ModelState.Remove(nameof(Employee.EmployeeNumber));

            if (!ModelState.IsValid)
                return View(model);

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            // تعديل بيانات الموظف
            employee.FirstName = model.FirstName;
            employee.FatherName = model.FatherName;
            employee.LastName = model.LastName;
            employee.Phone = model.Phone;
            employee.Address = model.Address;

            // تحديث رقم الهاتف بالحساب أيضاً
            var account = await _userManager.FindByIdAsync(
                employee.AccountId.ToString());

            if (account != null)
            {
                account.PhoneNumber = model.Phone;

                var result =
                    await _userManager.UpdateAsync(account);

                if (!result.Succeeded)
                {
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

            return RedirectToAction(
                nameof(Details),
                new { id = employee.Id });
        }

        // فتح صفحة تأكيد الحذف
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            return View(employee);
        }

        // تنفيذ حذف الموظف
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            // منع الحذف إذا كان الموظف مرتبطاً بمقررات
            bool hasOfferings =
                await _context.CourseOfferings.AnyAsync(o =>
                    o.EmployeeId == id);

            if (hasOfferings)
            {
                TempData["Error"] =
                    "لا يمكن حذف الموظف لأنه مرتبط بمقررات مطروحة";

                return RedirectToAction(nameof(Index));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var account = await _userManager.FindByIdAsync(
                employee.AccountId.ToString());

            // حذف سجل الموظف
            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            // حذف حساب Identity المرتبط
            if (account != null)
            {
                var result =
                    await _userManager.DeleteAsync(account);

                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "تعذر حذف حساب الموظف";

                    return RedirectToAction(nameof(Index));
                }
            }

            await transaction.CommitAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}