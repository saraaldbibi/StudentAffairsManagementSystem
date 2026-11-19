
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class RegistrationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        private const string Registered = "Registered";
        private const string Cancelled = "Cancelled";

        public RegistrationsController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // التحقق من فترة التسجيل المعتمدة
        // ==========================================
        private static bool IsRegistrationOpen(
            Semester? semester)
        {
            if (semester == null ||
                !semester.IsActive ||
                !semester.IsRegistrationOpen ||
                !semester.RegistrationStartDate.HasValue ||
                !semester.RegistrationEndDate.HasValue)
            {
                return false;
            }

            // يجب أن يتوافق توقيت الخادم مع توقيت الجامعة
            DateTime now = DateTime.Now;

            return now >= semester.RegistrationStartDate.Value &&
                   now <= semester.RegistrationEndDate.Value;
        }

        // ==========================================
        // الحصول على الفصل الدراسي الفعال
        // ==========================================
        private async Task<Semester?> GetActiveSemesterAsync()
        {
            return await _context.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();
        }

        // ==========================================
        // تسجيل المقررات: البحث عن الطالب
        // GET: /Registrations
        // ==========================================
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // ==========================================
        // إدارة التسجيلات: صفحة مستقلة
        // GET: /Registrations/Manage
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Manage(
            string? search,
            string? status)
        {
            search = search?.Trim();
            status = status?.Trim();

            var query = _context.Registrations
                .Include(r => r.Student)
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Course)
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Semester)
                .AsNoTracking()
                .AsQueryable();

            // البحث بالرقم الجامعي أو اسم الطالب أو المقرر
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r =>
                    r.Student.UniversityNumber.Contains(search) ||
                    r.Student.FirstName.Contains(search) ||
                    r.Student.LastName.Contains(search) ||
                    r.CourseOffering.Course.Name.Contains(search) ||
                    r.CourseOffering.Course.Code.Contains(search));
            }

            // التصفية حسب حالة التسجيل
            if (status == Registered || status == Cancelled)
            {
                query = query.Where(r => r.Status == status);
            }

            var registrations = await query
                .OrderByDescending(r => r.RegisteredAt)
                .ThenBy(r => r.Student.UniversityNumber)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;

            // الإحصائيات تخص نتائج البحث الحالية
            ViewBag.TotalRegistrations = registrations.Count;

            ViewBag.RegisteredCount = registrations.Count(
                r => r.Status == Registered);

            ViewBag.CancelledCount = registrations.Count(
                r => r.Status == Cancelled);

            return View(registrations);
        }

        // ==========================================
        // صفحة إدارة مقررات طالب محدد
        // GET: /Registrations/StudentCourses
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> StudentCourses(
            string? universityNumber)
        {
            universityNumber = universityNumber?.Trim();

            if (string.IsNullOrWhiteSpace(universityNumber))
            {
                TempData["Error"] =
                    "يرجى إدخال الرقم الجامعي للطالب.";

                return RedirectToAction(nameof(Index));
            }

            var student = await _context.Students
                .Include(s => s.Department)
                    .ThenInclude(d => d.Faculty)
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.UniversityNumber == universityNumber &&
                    !s.IsDeleted);

            if (student == null)
            {
                TempData["Error"] =
                    "لم يتم العثور على طالب بهذا الرقم الجامعي.";

                return RedirectToAction(nameof(Index));
            }

            var activeSemester =
                await GetActiveSemesterAsync();

            var registrations = new List<Registration>();
            var offerings = new List<CourseOffering>();
            var registeredCounts = new Dictionary<long, int>();

            decimal? pricePerHour = null;

            if (activeSemester != null)
            {
                registrations = await _context.Registrations
                    .Include(r => r.CourseOffering)
                        .ThenInclude(o => o.Course)
                    .Include(r => r.CourseOffering)
                        .ThenInclude(o => o.Semester)
                    .AsNoTracking()
                    .Where(r =>
                        r.StudentId == student.Id &&
                        r.CourseOffering.SemesterId ==
                            activeSemester.Id)
                    .OrderBy(r => r.CourseOffering.Course.Name)
                    .ToListAsync();

                offerings = await _context.CourseOfferings
                    .Include(o => o.Course)
                    .Include(o => o.Semester)
                    .AsNoTracking()
                    .Where(o =>
                        o.SemesterId == activeSemester.Id &&
                        o.Course.DepartmentId ==
                            student.DepartmentId)
                    .OrderBy(o => o.Course.Name)
                    .ToListAsync();

                var offeringIds = offerings
                    .Select(o => o.Id)
                    .ToList();

                registeredCounts = await _context.Registrations
                    .AsNoTracking()
                    .Where(r =>
                        offeringIds.Contains(r.CourseOfferingId) &&
                        r.Status == Registered)
                    .GroupBy(r => r.CourseOfferingId)
                    .Select(g => new
                    {
                        OfferingId = g.Key,
                        Count = g.Count()
                    })
                    .ToDictionaryAsync(
                        x => x.OfferingId,
                        x => x.Count);

                pricePerHour = await _context.HourPrices
                    .AsNoTracking()
                    .Where(h =>
                        h.DepartmentId == student.DepartmentId &&
                        h.SemesterId == activeSemester.Id)
                    .Select(h => (decimal?)h.PricePerHour)
                    .FirstOrDefaultAsync();
            }

            var activeRegistrations = registrations
                .Where(r => r.Status == Registered)
                .ToList();

            int totalHours = activeRegistrations.Sum(r =>
                r.CourseOffering.Course.CreditHours);

            ViewBag.ActiveSemester = activeSemester;
            ViewBag.Registrations = registrations;
            ViewBag.Offerings = offerings;
            ViewBag.RegisteredCounts = registeredCounts;

            ViewBag.IsRegistrationOpen =
                IsRegistrationOpen(activeSemester);

            ViewBag.PricePerHour = pricePerHour;
            ViewBag.TotalHours = totalHours;

            ViewBag.EstimatedFees = pricePerHour.HasValue
                ? totalHours * pricePerHour.Value
                : (decimal?)null;

            return View(student);
        }

        // ==========================================
        // تفاصيل تسجيل محدد
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var registration = await _context.Registrations
                .Include(r => r.Student)
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Course)
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Semester)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
                return NotFound();

            return View(registration);
        }

        // ==========================================
        // صفحة الإضافة القديمة
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadLists();

            return View(new Registration());
        }

        // ==========================================
        // تسجيل مقرر للطالب
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("StudentId,CourseOfferingId")]
            Registration model)
        {
            ModelState.Remove(nameof(Registration.Student));
            ModelState.Remove(nameof(Registration.CourseOffering));
            ModelState.Remove(nameof(Registration.RegisteredAt));
            ModelState.Remove(nameof(Registration.Status));

            string? universityNumber = await _context.Students
                .AsNoTracking()
                .Where(s =>
                    s.Id == model.StudentId &&
                    !s.IsDeleted)
                .Select(s => s.UniversityNumber)
                .FirstOrDefaultAsync();

            IActionResult ReturnToPage()
            {
                if (!string.IsNullOrWhiteSpace(universityNumber))
                {
                    return RedirectToAction(
                        nameof(StudentCourses),
                        new { universityNumber });
                }

                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] =
                    "بيانات التسجيل غير صحيحة.";

                return ReturnToPage();
            }

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var student = await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.Id == model.StudentId &&
                        !s.IsDeleted);

                if (student == null)
                {
                    TempData["Error"] =
                        "الطالب غير موجود أو محذوف من النظام.";

                    return ReturnToPage();
                }

                var offering = await _context.CourseOfferings
                    .Include(o => o.Course)
                    .Include(o => o.Semester)
                    .FirstOrDefaultAsync(o =>
                        o.Id == model.CourseOfferingId);

                if (offering == null)
                {
                    TempData["Error"] =
                        "المقرر المطروح غير موجود.";

                    return ReturnToPage();
                }

                var activeSemester =
                    await GetActiveSemesterAsync();

                if (activeSemester == null ||
                    offering.SemesterId != activeSemester.Id)
                {
                    TempData["Error"] =
                        "يمكن تسجيل مقررات الفصل الدراسي الحالي فقط.";

                    return ReturnToPage();
                }

                // منع التسجيل خارج الفترة المحددة
                if (!IsRegistrationOpen(offering.Semester))
                {
                    TempData["Error"] =
                        "فترة تسجيل المقررات غير متاحة حاليًا.";

                    return ReturnToPage();
                }

                // التحقق من القسم الدراسي
                if (offering.Course.DepartmentId !=
                    student.DepartmentId)
                {
                    TempData["Error"] =
                        "المقرر المختار غير تابع للقسم الدراسي للطالب.";

                    return ReturnToPage();
                }

                // منع تسجيل المقرر نفسه في أكثر من شعبة
                bool alreadyRegistered =
                    await _context.Registrations.AnyAsync(r =>
                        r.StudentId == student.Id &&
                        r.Status == Registered &&
                        r.CourseOffering.CourseId ==
                            offering.CourseId &&
                        r.CourseOffering.SemesterId ==
                            offering.SemesterId);

                if (alreadyRegistered)
                {
                    TempData["Error"] =
                        "الطالب مسجل في هذا المقرر مسبقًا.";

                    return ReturnToPage();
                }

                // التحقق من المقاعد المتاحة
                int registeredCount =
                    await _context.Registrations.CountAsync(r =>
                        r.CourseOfferingId == offering.Id &&
                        r.Status == Registered);

                if (offering.Capacity <= 0 ||
                    registeredCount >= offering.Capacity)
                {
                    TempData["Error"] =
                        "لا توجد مقاعد شاغرة في هذا المقرر.";

                    return ReturnToPage();
                }

                // إعادة تفعيل سجل ملغى أو إنشاء سجل جديد
                var existingRegistration =
                    await _context.Registrations.FirstOrDefaultAsync(r =>
                        r.StudentId == student.Id &&
                        r.CourseOfferingId == offering.Id);

                if (existingRegistration != null)
                {
                    if (existingRegistration.Status != Cancelled)
                    {
                        TempData["Error"] =
                            "لا يمكن تسجيل المقرر بسبب حالة تسجيل سابقة.";

                        return ReturnToPage();
                    }

                    existingRegistration.Status = Registered;
                    existingRegistration.RegisteredAt =
                        DateTime.UtcNow;
                }
                else
                {
                    _context.Registrations.Add(new Registration
                    {
                        StudentId = student.Id,
                        CourseOfferingId = offering.Id,
                        RegisteredAt = DateTime.UtcNow,
                        Status = Registered
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    "تم تسجيل المقرر للطالب بنجاح.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "تعذر حفظ التسجيل بسبب تعارض في البيانات.";
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is Microsoft.Data.SqlClient.SqlException)
            {
                TempData["Error"] =
                    "تعذر إتمام التسجيل حاليًا. يرجى المحاولة مجددًا.";
            }

            return ReturnToPage();
        }

        // ==========================================
        // إلغاء التسجيل مع الاحتفاظ بالسجل
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(long id)
        {
            var registration = await _context.Registrations
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
                return NotFound();

            if (registration.Status != Registered)
            {
                TempData["Error"] =
                    "هذا التسجيل ملغى مسبقًا.";

                return RedirectToAction(
                    nameof(StudentCourses),
                    new
                    {
                        universityNumber =
                            registration.Student.UniversityNumber
                    });
            }

            registration.Status = Cancelled;

            try
            {
                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "تم إلغاء تسجيل المقرر مع الاحتفاظ بالسجل.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "تعذر إلغاء تسجيل المقرر.";
            }

            return RedirectToAction(
                nameof(StudentCourses),
                new
                {
                    universityNumber =
                        registration.Student.UniversityNumber
                });
        }

        // ==========================================
        // إعادة تفعيل تسجيل ملغى
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(long id)
        {
            string? universityNumber = null;

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var registration = await _context.Registrations
                    .Include(r => r.Student)
                    .Include(r => r.CourseOffering)
                        .ThenInclude(o => o.Course)
                    .Include(r => r.CourseOffering)
                        .ThenInclude(o => o.Semester)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (registration == null)
                    return NotFound();

                universityNumber =
                    registration.Student.UniversityNumber;

                if (registration.Status != Cancelled)
                {
                    TempData["Error"] =
                        "يمكن إعادة تفعيل التسجيلات الملغاة فقط.";
                }
                else if (registration.Student.IsDeleted)
                {
                    TempData["Error"] =
                        "لا يمكن إعادة تفعيل تسجيل طالب محذوف.";
                }
                else
                {
                    var offering = registration.CourseOffering;

                    var activeSemester =
                        await GetActiveSemesterAsync();

                    if (activeSemester == null ||
                        offering.SemesterId != activeSemester.Id ||
                        !IsRegistrationOpen(offering.Semester))
                    {
                        TempData["Error"] =
                            "إعادة التفعيل غير متاحة خارج فترة التسجيل.";
                    }
                    else if (offering.Course.DepartmentId !=
                             registration.Student.DepartmentId)
                    {
                        TempData["Error"] =
                            "المقرر غير تابع للقسم الدراسي للطالب.";
                    }
                    else
                    {
                        bool duplicateExists =
                            await _context.Registrations.AnyAsync(r =>
                                r.Id != registration.Id &&
                                r.StudentId ==
                                    registration.StudentId &&
                                r.Status == Registered &&
                                r.CourseOffering.CourseId ==
                                    offering.CourseId &&
                                r.CourseOffering.SemesterId ==
                                    offering.SemesterId);

                        int registeredCount =
                            await _context.Registrations.CountAsync(r =>
                                r.CourseOfferingId ==
                                    registration.CourseOfferingId &&
                                r.Status == Registered);

                        if (duplicateExists)
                        {
                            TempData["Error"] =
                                "الطالب مسجل في هذا المقرر في شعبة أخرى.";
                        }
                        else if (offering.Capacity <= 0 ||
                                 registeredCount >= offering.Capacity)
                        {
                            TempData["Error"] =
                                "لا توجد مقاعد شاغرة لإعادة التفعيل.";
                        }
                        else
                        {
                            registration.Status = Registered;
                            registration.RegisteredAt =
                                DateTime.UtcNow;

                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();

                            TempData["Success"] =
                                "تمت إعادة تفعيل التسجيل بنجاح.";
                        }
                    }
                }
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "تعذرت إعادة التفعيل بسبب تعارض في البيانات.";
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is Microsoft.Data.SqlClient.SqlException)
            {
                TempData["Error"] =
                    "تعذرت إعادة التفعيل حاليًا.";
            }

            if (!string.IsNullOrWhiteSpace(universityNumber))
            {
                return RedirectToAction(
                    nameof(StudentCourses),
                    new { universityNumber });
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // تجهيز قوائم صفحة الإضافة القديمة
        // ==========================================
        private async Task LoadLists()
        {
            var students = await _context.Students
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.UniversityNumber)
                .ToListAsync();

            var offerings = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .AsNoTracking()
                .Where(o => o.Semester.IsActive)
                .OrderBy(o => o.Course.Name)
                .ToListAsync();

            ViewBag.StudentId = new SelectList(
                students.Select(s => new
                {
                    s.Id,
                    DisplayName =
                        s.UniversityNumber + " - " +
                        s.FirstName + " " + s.LastName
                }),
                "Id",
                "DisplayName");

            ViewBag.CourseOfferingId = new SelectList(
                offerings.Select(o => new
                {
                    o.Id,
                    DisplayName =
                        o.Course.Name + " - " +
                        o.Semester.Name + " (" +
                        o.Semester.AcademicYear + ")"
                }),
                "Id",
                "DisplayName");
        }
    }
}
