
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<Account> _userManager;

        private const string Registered = "Registered";
        private const string Cancelled = "Cancelled";

        private const string ManagePermission =
            "Registrations.Manage";

        public RegistrationsController(
            ApplicationDbContext context,
            UserManager<Account> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // التحقق من صلاحية الموظف
        // ==========================================
        private async Task<bool> CanManageRegistrationsAsync()
        {
            if (User.IsInRole("Admin"))
                return true;

            if (!User.IsInRole("Employee"))
                return false;

            string? userId = _userManager.GetUserId(User);

            if (!long.TryParse(userId, out long accountId))
                return false;

            bool employeeIsActive = await _context.Employees
                .AsNoTracking()
                .AnyAsync(e =>
                    e.AccountId == accountId &&
                    !e.IsDeleted);

            if (!employeeIsActive)
                return false;

            return await _context.AccountPermissions
                .AsNoTracking()
                .AnyAsync(ap =>
                    ap.AccountId == accountId &&
                    ap.Permission.Code == ManagePermission &&
                    ap.IsActive &&
                    ap.RevokedAt == null);
        }

        // ==========================================
        // سبب إغلاق التسجيل
        // ==========================================
        private static string? GetRegistrationClosedReason(
            Semester? semester)
        {
            if (semester == null)
            {
                return "لا يوجد فصل دراسي نشط حاليًا.";
            }

            if (!semester.IsActive)
            {
                return "الفصل الدراسي المحدد غير نشط.";
            }

            if (!semester.IsRegistrationOpen)
            {
                return "تسجيل المقررات مغلق من قبل إدارة الجامعة.";
            }

            if (!semester.RegistrationStartDate.HasValue ||
                !semester.RegistrationEndDate.HasValue)
            {
                return "لم يتم تحديد فترة تسجيل المقررات لهذا الفصل.";
            }

            DateTime start =
                semester.RegistrationStartDate.Value;

            DateTime end =
                semester.RegistrationEndDate.Value;

            if (end < start)
            {
                return "تاريخ نهاية التسجيل يسبق تاريخ بدايته.";
            }

            // يعتمد النظام توقيت الخادم المحلي.
            // إذا كان تاريخ النهاية دون وقت محدد،
            // يتم اعتبار نهاية اليوم موعدًا لإغلاق التسجيل.
            if (end.TimeOfDay == TimeSpan.Zero)
            {
                end = end.Date.AddDays(1).AddTicks(-1);
            }

            DateTime now = DateTime.Now;

            if (now < start)
            {
                return "لم تبدأ فترة تسجيل المقررات بعد.";
            }

            if (now > end)
            {
                return "انتهت فترة تسجيل المقررات لهذا الفصل.";
            }

            return null;
        }

        private static bool IsRegistrationOpen(
            Semester? semester)
        {
            return GetRegistrationClosedReason(semester) == null;
        }

        // ==========================================
        // الفصل الدراسي النشط
        // ==========================================
        private async Task<Semester?> GetActiveSemesterAsync()
        {
            return await _context.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();
        }

        // ==========================================
        // البحث عن طالب
        // ==========================================
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // ==========================================
        // إدارة التسجيلات
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

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r =>
                    r.Student.UniversityNumber.Contains(search) ||
                    r.Student.FirstName.Contains(search) ||
                    r.Student.LastName.Contains(search) ||
                    r.CourseOffering.Course.Name.Contains(search) ||
                    r.CourseOffering.Course.Code.Contains(search));
            }

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

            ViewBag.TotalRegistrations = registrations.Count;

            ViewBag.RegisteredCount = registrations.Count(
                r => r.Status == Registered);

            ViewBag.CancelledCount = registrations.Count(
                r => r.Status == Cancelled);

            ViewBag.CanManageRegistrations =
                await CanManageRegistrationsAsync();

            return View(registrations);
        }

        // ==========================================
        // صفحة مقررات طالب محدد
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

            bool canManage =
                await CanManageRegistrationsAsync();

            string? closedReason =
                GetRegistrationClosedReason(activeSemester);

            ViewBag.ActiveSemester = activeSemester;
            ViewBag.Registrations = registrations;
            ViewBag.Offerings = offerings;
            ViewBag.RegisteredCounts = registeredCounts;

            // يبقى هذا المتغير متوافقًا مع الواجهة الحالية،
            // ويعكس أيضًا صلاحية الموظف.
            ViewBag.IsRegistrationOpen =
                closedReason == null && canManage;

            ViewBag.RegistrationClosedReason =
                closedReason;

            ViewBag.CanManageRegistrations = canManage;

            ViewBag.PricePerHour = pricePerHour;
            ViewBag.TotalHours = totalHours;

            ViewBag.EstimatedFees = pricePerHour.HasValue
                ? totalHours * pricePerHour.Value
                : (decimal?)null;

            return View(student);
        }

        // ==========================================
        // تفاصيل تسجيل
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
        // صفحة التسجيل القديمة
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!await CanManageRegistrationsAsync())
                return Forbid();

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
            if (!await CanManageRegistrationsAsync())
                return Forbid();

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

                string? closedReason =
                    GetRegistrationClosedReason(activeSemester);

                if (closedReason != null)
                {
                    TempData["Error"] = closedReason;
                    return ReturnToPage();
                }

                if (offering.Course.DepartmentId !=
                    student.DepartmentId)
                {
                    TempData["Error"] =
                        "المقرر المختار غير تابع للقسم الدراسي للطالب.";

                    return ReturnToPage();
                }

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
        // إلغاء تسجيل مقرر
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(long id)
        {
            if (!await CanManageRegistrationsAsync())
                return Forbid();

            var registration = await _context.Registrations
                .Include(r => r.Student)
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Semester)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
                return NotFound();

            string universityNumber =
                registration.Student.UniversityNumber;

            var activeSemester =
                await GetActiveSemesterAsync();

            if (activeSemester == null ||
                registration.CourseOffering.SemesterId !=
                    activeSemester.Id)
            {
                TempData["Error"] =
                    "يمكن تعديل تسجيلات الفصل الدراسي الحالي فقط.";

                return RedirectToAction(
                    nameof(StudentCourses),
                    new { universityNumber });
            }

            string? closedReason =
                GetRegistrationClosedReason(activeSemester);

            if (closedReason != null)
            {
                TempData["Error"] = closedReason;

                return RedirectToAction(
                    nameof(StudentCourses),
                    new { universityNumber });
            }

            if (registration.Status != Registered)
            {
                TempData["Error"] =
                    "هذا التسجيل ملغى مسبقًا.";

                return RedirectToAction(
                    nameof(StudentCourses),
                    new { universityNumber });
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
                new { universityNumber });
        }

        // ==========================================
        // إعادة تفعيل تسجيل ملغى
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(long id)
        {
            if (!await CanManageRegistrationsAsync())
                return Forbid();

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
                        offering.SemesterId != activeSemester.Id)
                    {
                        TempData["Error"] =
                            "يمكن إعادة تفعيل تسجيلات الفصل الحالي فقط.";
                    }
                    else
                    {
                        string? closedReason =
                            GetRegistrationClosedReason(activeSemester);

                        if (closedReason != null)
                        {
                            TempData["Error"] = closedReason;
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
        // قوائم صفحة التسجيل القديمة
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
