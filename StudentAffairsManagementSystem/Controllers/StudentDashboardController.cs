
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
    [Authorize(Roles = "Student")]
    public class StudentDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Account> _userManager;

        private const string Registered = "Registered";
        private const string Cancelled = "Cancelled";

        private const string RegisterPermission =
            "Students.RegisterCourses";

        public StudentDashboardController(
            ApplicationDbContext context,
            UserManager<Account> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // الطالب المرتبط بالحساب الحالي
        // ==========================================
        private async Task<Student?> GetCurrentStudentAsync()
        {
            string? accountIdText =
                _userManager.GetUserId(User);

            if (!long.TryParse(
                    accountIdText,
                    out long accountId))
            {
                return null;
            }

            return await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.AccountId == accountId &&
                    !s.IsDeleted);
        }

        // ==========================================
        // صلاحية تسجيل المقررات الممنوحة من الأدمن
        // ==========================================
        private async Task<bool> CanRegisterCoursesAsync(
            Student student)
        {
            return await _context.AccountPermissions
                .AsNoTracking()
                .AnyAsync(ap =>
                    ap.AccountId == student.AccountId &&
                    ap.Permission.Code == RegisterPermission &&
                    ap.IsActive &&
                    ap.RevokedAt == null);
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
        // سبب عدم إتاحة فترة التسجيل
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
                return "الفصل الدراسي غير نشط.";
            }

            if (!semester.IsRegistrationOpen)
            {
                return "تسجيل المقررات مغلق من قبل إدارة الجامعة.";
            }

            if (!semester.RegistrationStartDate.HasValue ||
                !semester.RegistrationEndDate.HasValue)
            {
                return "لم تحدد إدارة الجامعة فترة التسجيل بعد.";
            }

            DateTime start =
                semester.RegistrationStartDate.Value;

            DateTime end =
                semester.RegistrationEndDate.Value;

            if (end < start)
            {
                return "تاريخ نهاية التسجيل يسبق تاريخ بدايته.";
            }

            // إذا لم يُحدد وقت لنهاية التسجيل،
            // نعتبر نهاية اليوم آخر موعد للتسجيل.
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
                return "انتهت فترة تسجيل المقررات.";
            }

            return null;
        }

        // ==========================================
        // بوابة الطالب الرئيسية
        // GET: /StudentDashboard
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentStudent =
                await GetCurrentStudentAsync();

            if (currentStudent == null)
                return NotFound();

            var student = await _context.Students
                .Include(s => s.Department)
                .Include(s => s.AdmissionSemester)
                .Include(s => s.GraduationSemester)
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == currentStudent.Id &&
                    !s.IsDeleted);

            if (student == null)
                return NotFound();

            var registrations = await _context.Registrations
                .Where(r =>
                    r.StudentId == student.Id &&
                    r.Status == Registered)
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Course)
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Semester)
                .AsNoTracking()
                .ToListAsync();

            var activeSemester =
                await GetActiveSemesterAsync();

            var currentRegistrations =
                activeSemester == null
                    ? new List<Registration>()
                    : registrations
                        .Where(r =>
                            r.CourseOffering.SemesterId ==
                            activeSemester.Id)
                        .ToList();

            int currentCreditHours =
                currentRegistrations.Sum(r =>
                    r.CourseOffering.Course.CreditHours);

            decimal? pricePerHour = null;

            if (activeSemester != null)
            {
                pricePerHour = await _context.HourPrices
                    .AsNoTracking()
                    .Where(h =>
                        h.DepartmentId == student.DepartmentId &&
                        h.SemesterId == activeSemester.Id)
                    .Select(h => (decimal?)h.PricePerHour)
                    .FirstOrDefaultAsync();
            }

            decimal? estimatedFees =
                pricePerHour.HasValue
                    ? currentCreditHours * pricePerHour.Value
                    : null;

            ViewBag.ActiveSemester = activeSemester;
            ViewBag.CurrentRegistrations =
                currentRegistrations;

            ViewBag.AllRegistrations = registrations;
            ViewBag.CurrentCreditHours =
                currentCreditHours;

            ViewBag.PricePerHour = pricePerHour;
            ViewBag.EstimatedFees = estimatedFees;

            return View(student);
        }

        // ==========================================
        // المقررات المتاحة للطالب
        // GET: /StudentDashboard/Courses
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Courses()
        {
            var student =
                await GetCurrentStudentAsync();

            if (student == null)
                return NotFound();

            var activeSemester =
                await GetActiveSemesterAsync();

            bool hasPermission =
                await CanRegisterCoursesAsync(student);

            string? closedReason =
                GetRegistrationClosedReason(activeSemester);

            bool registrationOpen =
                hasPermission && closedReason == null;

            ViewBag.ActiveSemester = activeSemester;

            ViewBag.HasRegistrationPermission =
                hasPermission;

            ViewBag.CanRegisterCourses =
                registrationOpen;

            ViewBag.IsRegistrationOpen =
                registrationOpen;

            ViewBag.RegistrationMessage =
                !hasPermission
                    ? "لم تمنحك إدارة الجامعة صلاحية تسجيل المقررات الدراسية."
                    : closedReason ??
                      "تسجيل المقررات متاح حاليًا.";

            ViewBag.RegisteredOfferingIds =
                new List<long>();

            ViewBag.RegisteredCourseIds =
                new List<long>();

            ViewBag.RegisteredCounts =
                new Dictionary<long, int>();

            if (activeSemester == null)
            {
                return View(new List<CourseOffering>());
            }

            var offerings =
                await _context.CourseOfferings
                    .AsNoTracking()
                    .Include(o => o.Course)
                    .Include(o => o.Semester)
                    .Where(o =>
                        o.SemesterId == activeSemester.Id &&
                        o.Course.DepartmentId ==
                            student.DepartmentId)
                    .OrderBy(o => o.Course.Name)
                    .ToListAsync();

            var registeredOfferingIds =
                await _context.Registrations
                    .AsNoTracking()
                    .Where(r =>
                        r.StudentId == student.Id &&
                        r.Status == Registered &&
                        r.CourseOffering.SemesterId ==
                            activeSemester.Id)
                    .Select(r => r.CourseOfferingId)
                    .ToListAsync();

            var registeredCourseIds =
                await _context.Registrations
                    .AsNoTracking()
                    .Where(r =>
                        r.StudentId == student.Id &&
                        r.Status == Registered &&
                        r.CourseOffering.SemesterId ==
                            activeSemester.Id)
                    .Select(r => r.CourseOffering.CourseId)
                    .ToListAsync();

            var offeringIds = offerings
                .Select(o => o.Id)
                .ToList();

            var registeredCounts =
                await _context.Registrations
                    .AsNoTracking()
                    .Where(r =>
                        offeringIds.Contains(
                            r.CourseOfferingId) &&
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

            ViewBag.RegisteredOfferingIds =
                registeredOfferingIds;

            ViewBag.RegisteredCourseIds =
                registeredCourseIds;

            ViewBag.RegisteredCounts =
                registeredCounts;

            return View(offerings);
        }

        // ==========================================
        // تسجيل الطالب في مقرر
        // POST: /StudentDashboard/Register
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            long offeringId)
        {
            var student =
                await GetCurrentStudentAsync();

            if (student == null)
                return NotFound();

            // التحقق من صلاحية الطالب على الخادم
            if (!await CanRegisterCoursesAsync(student))
            {
                TempData["ErrorMessage"] =
                    "لم تمنحك إدارة الجامعة صلاحية تسجيل المقررات الدراسية.";

                return RedirectToAction(nameof(Courses));
            }

            if (offeringId <= 0)
            {
                TempData["ErrorMessage"] =
                    "المقرر المطلوب غير صالح.";

                return RedirectToAction(nameof(Courses));
            }

            try
            {
                await using var transaction =
                    await _context.Database
                        .BeginTransactionAsync(
                            IsolationLevel.Serializable);

                var offering =
                    await _context.CourseOfferings
                        .Include(o => o.Course)
                        .Include(o => o.Semester)
                        .FirstOrDefaultAsync(o =>
                            o.Id == offeringId);

                if (offering == null)
                {
                    TempData["ErrorMessage"] =
                        "المقرر المطلوب غير موجود.";

                    return RedirectToAction(nameof(Courses));
                }

                var activeSemester =
                    await GetActiveSemesterAsync();

                if (activeSemester == null ||
                    offering.SemesterId != activeSemester.Id)
                {
                    TempData["ErrorMessage"] =
                        "التسجيل متاح لمقررات الفصل الحالي فقط.";

                    return RedirectToAction(nameof(Courses));
                }

                // لا تكفي الصلاحية وحدها:
                // يجب أن تكون فترة التسجيل مفتوحة.
                string? closedReason =
                    GetRegistrationClosedReason(
                        activeSemester);

                if (closedReason != null)
                {
                    TempData["ErrorMessage"] =
                        closedReason;

                    return RedirectToAction(nameof(Courses));
                }

                if (offering.Course.DepartmentId !=
                    student.DepartmentId)
                {
                    TempData["ErrorMessage"] =
                        "هذا المقرر غير تابع لقسمك الدراسي.";

                    return RedirectToAction(nameof(Courses));
                }

                if (offering.Capacity <= 0)
                {
                    TempData["ErrorMessage"] =
                        "لا توجد مقاعد متاحة في هذه الشعبة.";

                    return RedirectToAction(nameof(Courses));
                }

                // منع تسجيل المقرر نفسه مرتين،
                // حتى لو كان له أكثر من شعبة.
                bool alreadyRegistered =
                    await _context.Registrations
                        .AnyAsync(r =>
                            r.StudentId == student.Id &&
                            r.Status == Registered &&
                            r.CourseOffering.CourseId ==
                                offering.CourseId &&
                            r.CourseOffering.SemesterId ==
                                offering.SemesterId);

                if (alreadyRegistered)
                {
                    TempData["ErrorMessage"] =
                        "أنت مسجل في هذا المقرر بالفعل.";

                    return RedirectToAction(nameof(Courses));
                }

                int registeredCount =
                    await _context.Registrations
                        .CountAsync(r =>
                            r.CourseOfferingId == offering.Id &&
                            r.Status == Registered);

                if (registeredCount >= offering.Capacity)
                {
                    TempData["ErrorMessage"] =
                        "اكتملت المقاعد المتاحة لهذا المقرر.";

                    return RedirectToAction(nameof(Courses));
                }

                // إعادة تفعيل تسجيل ملغى
                // أو إنشاء تسجيل جديد.
                var existingRegistration =
                    await _context.Registrations
                        .FirstOrDefaultAsync(r =>
                            r.StudentId == student.Id &&
                            r.CourseOfferingId == offering.Id);

                if (existingRegistration != null)
                {
                    if (existingRegistration.Status != Cancelled)
                    {
                        TempData["ErrorMessage"] =
                            "لا يمكن تسجيل المقرر بسبب حالة تسجيل سابقة.";

                        return RedirectToAction(nameof(Courses));
                    }

                    existingRegistration.Status =
                        Registered;

                    existingRegistration.RegisteredAt =
                        DateTime.UtcNow;
                }
                else
                {
                    _context.Registrations.Add(
                        new Registration
                        {
                            StudentId = student.Id,
                            CourseOfferingId = offering.Id,
                            RegisteredAt = DateTime.UtcNow,
                            Status = Registered
                        });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    "تم تسجيل المقرر بنجاح.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "تعذر حفظ التسجيل بسبب تعارض في البيانات.";
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is Microsoft.Data.SqlClient.SqlException)
            {
                TempData["ErrorMessage"] =
                    "تعذر إتمام التسجيل حاليًا. يرجى المحاولة مجددًا.";
            }

            return RedirectToAction(nameof(Courses));
        }

        // ==========================================
        // الرسوم الجامعية
        // GET: /StudentDashboard/Fees
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Fees()
        {
            var student =
                await GetCurrentStudentAsync();

            if (student == null)
                return NotFound();

            var activeSemester =
                await GetActiveSemesterAsync();

            ViewBag.ActiveSemester = activeSemester;

            var registrations =
                new List<Registration>();

            decimal? pricePerHour = null;
            decimal? estimatedFees = null;

            if (activeSemester != null)
            {
                registrations =
                    await _context.Registrations
                        .AsNoTracking()
                        .Include(r => r.CourseOffering)
                            .ThenInclude(o => o.Course)
                        .Where(r =>
                            r.StudentId == student.Id &&
                            r.Status == Registered &&
                            r.CourseOffering.SemesterId ==
                                activeSemester.Id)
                        .ToListAsync();

                pricePerHour =
                    await _context.HourPrices
                        .AsNoTracking()
                        .Where(h =>
                            h.DepartmentId ==
                                student.DepartmentId &&
                            h.SemesterId ==
                                activeSemester.Id)
                        .Select(h =>
                            (decimal?)h.PricePerHour)
                        .FirstOrDefaultAsync();

                if (pricePerHour.HasValue)
                {
                    int totalHours =
                        registrations.Sum(r =>
                            r.CourseOffering.Course.CreditHours);

                    estimatedFees =
                        totalHours * pricePerHour.Value;
                }
            }

            ViewBag.Student = student;
            ViewBag.Registrations = registrations;
            ViewBag.PricePerHour = pricePerHour;
            ViewBag.EstimatedFees = estimatedFees;

            return View();
        }
    }
}
