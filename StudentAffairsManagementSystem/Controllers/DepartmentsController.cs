
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;
using StudentAffairsManagementSystem.Permissions;
using StudentAffairsManagementSystem.Services;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class DepartmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PermissionService _permissionService;

        public DepartmentsController(
            ApplicationDbContext context,
            PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        // =====================================
        // Check department editing permission
        // =====================================
        private async Task<bool> CanEditDepartmentsAsync()
        {
            return await _permissionService.HasPermissionAsync(
                User,
                PermissionCodes.DepartmentsEdit);
        }

        // =====================================
        // View all departments
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .Include(d => d.Faculty)
                .OrderBy(d => d.Name)
                .ToListAsync();

            return View(departments);
        }

        // =====================================
        // Department details
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var department = await _context.Departments
                .AsNoTracking()
                .Include(d => d.Faculty)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
            {
                return NotFound();
            }

            return View(department);
        }

        // =====================================
        // Create department - Admin only
        // =====================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadFaculties();

            return View(new Department());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,Code,FacultyId,IsActive")]
            Department department)
        {
            ModelState.Remove(nameof(Department.Faculty));

            department.Name =
                department.Name?.Trim() ?? "";

            department.Code =
                department.Code?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(department.Name))
            {
                ModelState.AddModelError(
                    nameof(Department.Name),
                    "اسم القسم مطلوب");
            }

            if (department.Code.Length != 2 ||
                !department.Code.All(c => c >= '0' && c <= '9'))
            {
                ModelState.AddModelError(
                    nameof(Department.Code),
                    "رمز القسم يجب أن يتكون من رقمين فقط");
            }

            bool facultyExists = await _context.Faculties
                .AnyAsync(f =>
                    f.Id == department.FacultyId &&
                    f.IsActive);

            if (!facultyExists)
            {
                ModelState.AddModelError(
                    nameof(Department.FacultyId),
                    "الكلية غير موجودة أو غير فعالة");
            }

            bool codeExists = await _context.Departments
                .AnyAsync(d => d.Code == department.Code);

            if (codeExists)
            {
                ModelState.AddModelError(
                    nameof(Department.Code),
                    "رمز القسم مستخدم مسبقاً");
            }

            if (!ModelState.IsValid)
            {
                await LoadFaculties();
                return View(department);
            }

            _context.Departments.Add(department);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    nameof(Department.Code),
                    "تعذر حفظ القسم. قد يكون رمز القسم مستخدماً مسبقاً");

                await LoadFaculties();
                return View(department);
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // Edit department
        // Admin or authorized Employee
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            if (!await CanEditDepartmentsAsync())
            {
                return Forbid();
            }

            var department = await _context.Departments
                .FindAsync(id);

            if (department == null)
            {
                return NotFound();
            }

            await LoadFaculties();

            return View(department);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind("Id,Name,Code,FacultyId,IsActive")]
            Department department)
        {
            // Check permission again on POST
            if (!await CanEditDepartmentsAsync())
            {
                return Forbid();
            }

            if (id != department.Id)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(Department.Faculty));

            department.Name =
                department.Name?.Trim() ?? "";

            department.Code =
                department.Code?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(department.Name))
            {
                ModelState.AddModelError(
                    nameof(Department.Name),
                    "اسم القسم مطلوب");
            }

            if (department.Code.Length != 2 ||
                !department.Code.All(c => c >= '0' && c <= '9'))
            {
                ModelState.AddModelError(
                    nameof(Department.Code),
                    "رمز القسم يجب أن يتكون من رقمين فقط");
            }

            bool facultyExists = await _context.Faculties
                .AnyAsync(f =>
                    f.Id == department.FacultyId &&
                    f.IsActive);

            if (!facultyExists)
            {
                ModelState.AddModelError(
                    nameof(Department.FacultyId),
                    "الكلية غير موجودة أو غير فعالة");
            }

            bool codeExists = await _context.Departments
                .AnyAsync(d =>
                    d.Code == department.Code &&
                    d.Id != id);

            if (codeExists)
            {
                ModelState.AddModelError(
                    nameof(Department.Code),
                    "رمز القسم مستخدم بقسم آخر");
            }

            var existingDepartment = await _context.Departments
                .FindAsync(id);

            if (existingDepartment == null)
            {
                return NotFound();
            }

            // Do not change department code
            // if any students are linked to it
            if (existingDepartment.Code != department.Code)
            {
                bool hasStudents = await _context.Students
                    .AnyAsync(s => s.DepartmentId == id);

                if (hasStudents)
                {
                    ModelState.AddModelError(
                        nameof(Department.Code),
                        "لا يمكن تغيير رمز القسم بعد تسجيل طلاب فيه");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadFaculties();
                return View(department);
            }

            existingDepartment.Name = department.Name;
            existingDepartment.Code = department.Code;
            existingDepartment.FacultyId = department.FacultyId;
            existingDepartment.IsActive = department.IsActive;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر حفظ التعديلات. تحققوا من البيانات وحاولوا مجدداً");

                await LoadFaculties();
                return View(department);
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // Delete department - Admin only
        // =====================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            var department = await _context.Departments
                .AsNoTracking()
                .Include(d => d.Faculty)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
            {
                return NotFound();
            }

            return View(department);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var department = await _context.Departments
                .FindAsync(id);

            if (department == null)
            {
                return NotFound();
            }

            bool hasStudents = await _context.Students
                .AnyAsync(s => s.DepartmentId == id);

            if (hasStudents)
            {
                TempData["Error"] =
                    "لا يمكن حذف القسم لأنه مرتبط بطلاب";

                return RedirectToAction(nameof(Index));
            }

            bool hasCourses = await _context.Courses
                .AnyAsync(c => c.DepartmentId == id);

            if (hasCourses)
            {
                TempData["Error"] =
                    "لا يمكن حذف القسم لأنه مرتبط بمقررات";

                return RedirectToAction(nameof(Index));
            }

            bool hasHourPrices = await _context.HourPrices
                .AnyAsync(h => h.DepartmentId == id);

            if (hasHourPrices)
            {
                TempData["Error"] =
                    "لا يمكن حذف القسم لأنه مرتبط بأسعار ساعات";

                return RedirectToAction(nameof(Index));
            }

            _context.Departments.Remove(department);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "تعذر حذف القسم لأنه قد يكون مرتبطاً ببيانات أخرى";

                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // Load active faculties
        // =====================================
        private async Task LoadFaculties()
        {
            var faculties = await _context.Faculties
                .AsNoTracking()
                .Where(f => f.IsActive)
                .OrderBy(f => f.Name)
                .ToListAsync();

            ViewBag.FacultyId = new SelectList(
                faculties,
                "Id",
                "Name");
        }
    }
}
