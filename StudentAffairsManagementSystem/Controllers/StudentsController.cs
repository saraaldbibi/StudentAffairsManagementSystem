
using System;
using System.Data;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;
using StudentAffairsManagementSystem.Permissions;
using StudentAffairsManagementSystem.Services;
using StudentAffairsManagementSystem.ViewModels;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Account> _userManager;
        private readonly PermissionService _permissionService;

        public StudentsController(
            ApplicationDbContext context,
            UserManager<Account> userManager,
            PermissionService permissionService)
        {
            _context = context;
            _userManager = userManager;
            _permissionService = permissionService;
        }

        // ==========================================
        // عرض الطلاب غير المحذوفين
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var students = await _context.Students
                .Where(s => !s.IsDeleted)
                .Include(s => s.Department)
                .Include(s => s.AdmissionSemester)
                .AsNoTracking()
                .OrderBy(s => s.FirstName)
                .ThenBy(s => s.LastName)
                .ToListAsync();

            ViewBag.CanDeleteStudents =
                await _permissionService.CanDeleteStudentsAsync(User);

            return View(students);
        }

        // ==========================================
        // تفاصيل الطالب
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var student = await _context.Students
                .Where(s => !s.IsDeleted)
                .Include(s => s.Department)
                .Include(s => s.AdmissionSemester)
                .Include(s => s.GraduationSemester)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
            {
                return NotFound();
            }

            ViewBag.CanDeleteStudents =
                await _permissionService.CanDeleteStudentsAsync(User);

            return View(student);
        }

        // ==========================================
        // فتح صفحة إضافة طالب
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadStudentLists();
            return View(new CreateStudentViewModel());
        }

        // ==========================================
        // إضافة الطالب وإنشاء حسابه
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateStudentViewModel model)
        {
            var department = await _context.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(d =>
                    d.Id == model.DepartmentId &&
                    d.IsActive);

            if (department == null)
            {
                ModelState.AddModelError(
                    nameof(model.DepartmentId),
                    "القسم غير موجود أو غير فعال");
            }
            else if (
                string.IsNullOrEmpty(department.Code) ||
                department.Code.Length != 2 ||
                !department.Code.All(c =>
                    c >= '0' && c <= '9'))
            {
                ModelState.AddModelError(
                    nameof(model.DepartmentId),
                    "رمز القسم يجب أن يكون رقمين بالضبط");
            }

            bool admissionSemesterExists =
                await _context.Semesters.AnyAsync(s =>
                    s.Id == model.AdmissionSemesterId);

            if (!admissionSemesterExists)
            {
                ModelState.AddModelError(
                    nameof(model.AdmissionSemesterId),
                    "فصل القبول غير موجود");
            }

            if (model.GraduationSemesterId.HasValue)
            {
                bool graduationSemesterExists =
                    await _context.Semesters.AnyAsync(s =>
                        s.Id == model.GraduationSemesterId.Value);

                if (!graduationSemesterExists)
                {
                    ModelState.AddModelError(
                        nameof(model.GraduationSemesterId),
                        "فصل التخرج غير موجود");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadStudentLists();
                return View(model);
            }

            DateTime registrationDate = DateTime.Now;

            string registrationMonth =
                registrationDate.Month.ToString(
                    "D2",
                    CultureInfo.InvariantCulture);

            string registrationYear =
                (registrationDate.Year % 100).ToString(
                    "D2",
                    CultureInfo.InvariantCulture);

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            string departmentCode = department!.Code;

            // نحتسب أرقام الطلاب المحذوفين أيضًا
            // حتى لا نعيد استخدام رقم جامعي قديم
            var existingNumbers = await _context.Students
                .Where(s => s.UniversityNumber.StartsWith(
                    departmentCode))
                .Select(s => s.UniversityNumber)
                .ToListAsync();

            int lastSequence = existingNumbers
                .Where(n =>
                    n.Length == 9 &&
                    n.StartsWith(departmentCode))
                .Select(n =>
                    int.TryParse(
                        n.Substring(6, 3),
                        out int value)
                        ? value
                        : 0)
                .DefaultIfEmpty(0)
                .Max();

            int nextSequence = lastSequence + 1;

            if (nextSequence > 999)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "وصل القسم إلى الحد الأقصى من الأرقام التسلسلية");

                await LoadStudentLists();
                return View(model);
            }

            string universityNumber =
                departmentCode +
                registrationMonth +
                registrationYear +
                nextSequence.ToString(
                    "D3",
                    CultureInfo.InvariantCulture);

            var account = new Account
            {
                UserName = universityNumber,
                PhoneNumber = model.Phone,
                Role = "Student"
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

                await LoadStudentLists();
                return View(model);
            }

            var roleResult =
                await _userManager.AddToRoleAsync(
                    account,
                    "Student");

            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();

                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                await LoadStudentLists();
                return View(model);
            }

            var student = new Student
            {
                UniversityNumber = universityNumber,

                FirstName = model.FirstName.Trim(),
                FatherName = model.FatherName.Trim(),
                MotherName = model.MotherName.Trim(),
                LastName = model.LastName.Trim(),

                DepartmentId = model.DepartmentId,
                AccountId = account.Id,

                AdmissionSemesterId =
                    model.AdmissionSemesterId,

                GraduationSemesterId =
                    model.GraduationSemesterId,

                Phone = model.Phone,
                Address = model.Address,
                Governorate = model.Governorate,

                BirthDate = model.BirthDate,
                Gender = model.Gender,
                NationalId = model.NationalId,
                Nationality = model.Nationality,

                Status = model.Status,
                GraduationAverage =
                    model.GraduationAverage,

                PersonalImage = model.PersonalImage,
                NationalIdImage = model.NationalIdImage,
                CertificateImage = model.CertificateImage,

                IsDeleted = false,
                DeletedAt = null
            };

            _context.Students.Add(student);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(
                nameof(Details),
                new { id = student.Id });
        }

        // ==========================================
        // فتح صفحة تعديل الطالب
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    !s.IsDeleted);

            if (student == null)
            {
                return NotFound();
            }

            var model = new EditStudentViewModel
            {
                Id = student.Id,
                UniversityNumber = student.UniversityNumber,

                FirstName = student.FirstName,
                FatherName = student.FatherName,
                MotherName = student.MotherName,
                LastName = student.LastName,

                BirthDate = student.BirthDate,
                Gender = student.Gender,
                NationalId = student.NationalId,
                Nationality = student.Nationality,

                DepartmentId = student.DepartmentId,
                AdmissionSemesterId =
                    student.AdmissionSemesterId,
                GraduationSemesterId =
                    student.GraduationSemesterId,

                Status = student.Status,
                GraduationAverage =
                    student.GraduationAverage,

                Phone = student.Phone,
                Address = student.Address,
                Governorate = student.Governorate,

                PersonalImage = student.PersonalImage,
                NationalIdImage = student.NationalIdImage,
                CertificateImage = student.CertificateImage
            };

            await LoadStudentLists();
            return View(model);
        }

        // ==========================================
        // حفظ تعديل الطالب
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            EditStudentViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var student = await _context.Students
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    !s.IsDeleted);

            if (student == null)
            {
                return NotFound();
            }

            model.UniversityNumber = student.UniversityNumber;

            ModelState.Remove(
                nameof(EditStudentViewModel.UniversityNumber));

            bool departmentExists =
                await _context.Departments.AnyAsync(d =>
                    d.Id == model.DepartmentId &&
                    d.IsActive);

            if (!departmentExists)
            {
                ModelState.AddModelError(
                    nameof(model.DepartmentId),
                    "القسم غير موجود أو غير فعال");
            }

            bool admissionSemesterExists =
                await _context.Semesters.AnyAsync(s =>
                    s.Id == model.AdmissionSemesterId);

            if (!admissionSemesterExists)
            {
                ModelState.AddModelError(
                    nameof(model.AdmissionSemesterId),
                    "فصل القبول غير موجود");
            }

            if (model.GraduationSemesterId.HasValue)
            {
                bool graduationSemesterExists =
                    await _context.Semesters.AnyAsync(s =>
                        s.Id == model.GraduationSemesterId.Value);

                if (!graduationSemesterExists)
                {
                    ModelState.AddModelError(
                        nameof(model.GraduationSemesterId),
                        "فصل التخرج غير موجود");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadStudentLists();
                return View(model);
            }

            student.FirstName = model.FirstName.Trim();
            student.FatherName = model.FatherName.Trim();
            student.MotherName = model.MotherName.Trim();
            student.LastName = model.LastName.Trim();

            student.BirthDate = model.BirthDate;
            student.Gender = model.Gender;
            student.NationalId = model.NationalId;
            student.Nationality = model.Nationality;

            student.DepartmentId = model.DepartmentId;
            student.AdmissionSemesterId =
                model.AdmissionSemesterId;
            student.GraduationSemesterId =
                model.GraduationSemesterId;

            student.Status = model.Status;
            student.GraduationAverage =
                model.GraduationAverage;

            student.Phone = model.Phone;
            student.Address = model.Address;
            student.Governorate = model.Governorate;

            student.PersonalImage = model.PersonalImage;
            student.NationalIdImage = model.NationalIdImage;
            student.CertificateImage = model.CertificateImage;

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Details),
                new { id = student.Id });
        }

        // ==========================================
        // صفحة تأكيد حذف الطالب
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            // التحقق من الصلاحية عند كل طلب
            bool canDelete =
                await _permissionService.HasPermissionAsync(
                    User,
                    PermissionCodes.StudentsDelete);

            if (!canDelete)
            {
                return Forbid();
            }

            var student = await _context.Students
                .Include(s => s.Department)
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    !s.IsDeleted);

            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        // ==========================================
        // تنفيذ الحذف الآمن
        // ==========================================
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            // التحقق من الصلاحية مجددًا عند التنفيذ
            bool canDelete =
                await _permissionService.HasPermissionAsync(
                    User,
                    PermissionCodes.StudentsDelete);

            if (!canDelete)
            {
                return Forbid();
            }

            var student = await _context.Students
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    !s.IsDeleted);

            if (student == null)
            {
                return NotFound();
            }

            var account = await _userManager.FindByIdAsync(
                student.AccountId.ToString(
                    CultureInfo.InvariantCulture));

            if (account == null)
            {
                TempData["ErrorMessage"] =
                    "حساب الطالب غير موجود، لم يتم الحذف.";

                return RedirectToAction(
                    nameof(Delete),
                    new { id });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            student.IsDeleted = true;
            student.DeletedAt = DateTime.UtcNow;

            // منع تسجيل الدخول الجديد
            account.LockoutEnabled = true;
            account.LockoutEnd = DateTimeOffset.MaxValue;

            var updateResult =
                await _userManager.UpdateAsync(account);

            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "تعذّر إيقاف حساب الطالب. لم يتم الحذف.";

                return RedirectToAction(
                    nameof(Delete),
                    new { id });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "تم حذف الطالب من القوائم وإيقاف حسابه مع الاحتفاظ بسجلاته الأكاديمية.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // تجهيز قوائم الأقسام والفصول
        // ==========================================
        private async Task LoadStudentLists()
        {
            ViewBag.Departments = await _context.Departments
                .Where(d => d.IsActive)
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .ToListAsync();

            ViewBag.Semesters = await _context.Semesters
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
