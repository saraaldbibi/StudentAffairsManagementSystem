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
using StudentAffairsManagementSystem.ViewModels;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Account> _userManager;

        public StudentsController(
            ApplicationDbContext context,
            UserManager<Account> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // عرض جميع الطلاب
        public async Task<IActionResult> Index()
        {
            var students = await _context.Students
                .Include(s => s.Department)
                .Include(s => s.AdmissionSemester)
                .AsNoTracking()
                .ToListAsync();

            return View(students);
        }

        // عرض تفاصيل طالب معين
        public async Task<IActionResult> Details(long id)
        {
            var student = await _context.Students
                .Include(s => s.Department)
                .Include(s => s.AdmissionSemester)
                .Include(s => s.GraduationSemester)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
                return NotFound();

            return View(student);
        }

        // فتح صفحة إضافة طالب
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadStudentLists();

            return View(new CreateStudentViewModel());
        }

        // حفظ الطالب وإنشاء حسابه
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateStudentViewModel model)
        {
            // التحقق من وجود القسم وأنه فعال
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
                    "رمز الفرع لازم يكون رقمين بالضبط");
            }

            // التحقق من فصل القبول
            bool admissionSemesterExists =
                await _context.Semesters.AnyAsync(s =>
                    s.Id == model.AdmissionSemesterId);

            if (!admissionSemesterExists)
            {
                ModelState.AddModelError(
                    nameof(model.AdmissionSemesterId),
                    "فصل القبول غير موجود");
            }

            // التحقق من فصل التخرج إذا تم اختياره
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

            // تاريخ إضافة الطالب على الموقع
            DateTime registrationDate = DateTime.Now;

            // الشهر من تاريخ الإضافة
            string registrationMonth =
                registrationDate.Month.ToString(
                    "D2",
                    CultureInfo.InvariantCulture);

            // آخر رقمين من سنة الإضافة
            string registrationYear =
                (registrationDate.Year % 100).ToString(
                    "D2",
                    CultureInfo.InvariantCulture);

            // معاملة لحفظ الحساب والدور والطالب معاً
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            string departmentCode = department!.Code;

            // جلب الأرقام الجامعية لنفس الفرع
            var existingNumbers = await _context.Students
                .Where(s => s.DepartmentId == department.Id)
                .Select(s => s.UniversityNumber)
                .ToListAsync();

            // تحديد أكبر تسلسل مستخدم
            int lastSequence = existingNumbers
                .Where(n =>
                    n != null &&
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

            // الحد الأقصى للتسلسل هو 999
            if (nextSequence > 999)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "وصل الفرع للحد الأقصى من الأرقام التسلسلية");

                await LoadStudentLists();
                return View(model);
            }

            // رمز الفرع + الشهر + السنة + التسلسل
            string universityNumber =
                departmentCode +
                registrationMonth +
                registrationYear +
                nextSequence.ToString(
                    "D3",
                    CultureInfo.InvariantCulture);

            // إنشاء حساب الطالب
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

            // ربط الحساب بدور Student الحقيقي
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

            // إنشاء سجل الطالب
            var student = new Student
            {
                UniversityNumber = universityNumber,

                FirstName = model.FirstName,
                FatherName = model.FatherName,
                MotherName = model.MotherName,
                LastName = model.LastName,

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
                CertificateImage = model.CertificateImage
            };

            _context.Students.Add(student);

            await _context.SaveChangesAsync();

            // تثبيت الحساب والدور والطالب معاً
            await transaction.CommitAsync();

            return RedirectToAction(
                nameof(Details),
                new { id = student.Id });
        }

        // تجهيز قوائم الأقسام والفصول
        private async Task LoadStudentLists()
        {
            ViewBag.Departments = await _context.Departments
                .Where(d => d.IsActive)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Semesters = await _context.Semesters
                .AsNoTracking()
                .ToListAsync();
        }
    }
}