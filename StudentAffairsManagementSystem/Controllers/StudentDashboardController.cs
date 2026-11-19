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

        public StudentDashboardController(
            ApplicationDbContext context,
            UserManager<Account> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // جلب الطالب المرتبط بالحساب الحالي
        // ==========================================
        private async Task<Student?> GetCurrentStudentAsync()
        {
            string? accountIdText = _userManager.GetUserId(User);

            if (!long.TryParse(accountIdText, out long accountId))
                return null;

            return await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.AccountId == accountId &&
                    !s.IsDeleted);
        }

        // ==========================================
        // هل التسجيل مفتوح الآن؟
        // التواريخ بتوقيت الجامعة المحلي
        // ==========================================
        private static bool IsRegistrationAvailable(Semester? semester)
        {
            if (semester == null)
                return false;

            if (!semester.IsActive ||
                !semester.IsRegistrationOpen ||
                !semester.RegistrationStartDate.HasValue ||
                !semester.RegistrationEndDate.HasValue)
            {
                return false;
            }

            DateTime now = DateTime.Now;

            return now >= semester.RegistrationStartDate.Value &&
                   now <= semester.RegistrationEndDate.Value;
        }

        // ==========================================
        // رسالة توضح سبب إغلاق التسجيل
        // ==========================================
        private static string GetRegistrationMessage(Semester? semester)
        {
            if (semester == null)
                return "لا يوجد فصل دراسي فعّال حاليًا.";

            if (!semester.IsActive)
                return "الفصل الدراسي غير فعّال.";

            if (!semester.IsRegistrationOpen)
                return "التسجيل مغلق حاليًا من إدارة الجامعة.";

            if (!semester.RegistrationStartDate.HasValue ||
                !semester.RegistrationEndDate.HasValue)
            {
                return "لم تحدد إدارة الجامعة فترة التسجيل بعد.";
            }

            DateTime now = DateTime.Now;

            if (now < semester.RegistrationStartDate.Value)
                return "لم تبدأ فترة تسجيل المقررات بعد.";

            if (now > semester.RegistrationEndDate.Value)
                return "انتهت فترة تسجيل المقررات.";

            return "التسجيل متاح حاليًا.";
        }

        // ==========================================
        // بوابة الطالب الرئيسية
        // GET: /StudentDashboard
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentStudent = await GetCurrentStudentAsync();

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
                    r.Status == "Registered")
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Course)
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Semester)
                .AsNoTracking()
                .ToListAsync();

            var activeSemester = await _context.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            var currentRegistrations = activeSemester == null
                ? new List<Registration>()
                : registrations
                    .Where(r =>
                        r.CourseOffering.SemesterId == activeSemester.Id)
                    .ToList();

            int currentCreditHours = currentRegistrations
                .Sum(r => r.CourseOffering.Course.CreditHours);

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

            decimal? estimatedFees = pricePerHour.HasValue
                ? currentCreditHours * pricePerHour.Value
                : null;

            ViewBag.ActiveSemester = activeSemester;
            ViewBag.CurrentRegistrations = currentRegistrations;
            ViewBag.AllRegistrations = registrations;
            ViewBag.CurrentCreditHours = currentCreditHours;
            ViewBag.PricePerHour = pricePerHour;
            ViewBag.EstimatedFees = estimatedFees;

            return View(student);
        }

        // ==========================================
        // عرض المقررات المتاحة للطالب
        // GET: /StudentDashboard/Courses
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Courses()
        {
            var student = await GetCurrentStudentAsync();

            if (student == null)
                return NotFound();

            var activeSemester = await _context.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            ViewBag.ActiveSemester = activeSemester;

            bool registrationOpen =
                IsRegistrationAvailable(activeSemester);

            ViewBag.IsRegistrationOpen = registrationOpen;

            ViewBag.RegistrationMessage =
                GetRegistrationMessage(activeSemester);

            ViewBag.RegisteredOfferingIds = new List<long>();
            ViewBag.RegisteredCourseIds = new List<long>();
            ViewBag.RegisteredCounts = new Dictionary<long, int>();

            if (activeSemester == null)
                return View(new List<CourseOffering>());

            var offerings = await _context.CourseOfferings
                .AsNoTracking()
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .Where(o =>
                    o.SemesterId == activeSemester.Id &&
                    o.Course.DepartmentId == student.DepartmentId)
                .OrderBy(o => o.Course.Name)
                .ToListAsync();

            var registeredOfferingIds = await _context.Registrations
                .AsNoTracking()
                .Where(r =>
                    r.StudentId == student.Id &&
                    r.Status == "Registered" &&
                    r.CourseOffering.SemesterId == activeSemester.Id)
                .Select(r => r.CourseOfferingId)
                .ToListAsync();

            var registeredCourseIds = await _context.Registrations
                .AsNoTracking()
                .Where(r =>
                    r.StudentId == student.Id &&
                    r.Status == "Registered" &&
                    r.CourseOffering.SemesterId == activeSemester.Id)
                .Select(r => r.CourseOffering.CourseId)
                .ToListAsync();

            var offeringIds = offerings
                .Select(o => o.Id)
                .ToList();

            var registeredCounts = await _context.Registrations
                .AsNoTracking()
                .Where(r =>
                    offeringIds.Contains(r.CourseOfferingId) &&
                    r.Status == "Registered")
                .GroupBy(r => r.CourseOfferingId)
                .Select(g => new
                {
                    OfferingId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.OfferingId, x => x.Count);

            ViewBag.RegisteredOfferingIds = registeredOfferingIds;
            ViewBag.RegisteredCourseIds = registeredCourseIds;
            ViewBag.RegisteredCounts = registeredCounts;

            return View(offerings);
        }

        // ==========================================
        // تسجيل الطالب في مقرر
        // POST: /StudentDashboard/Register
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(long offeringId)
        {
            var student = await GetCurrentStudentAsync();

            if (student == null)
                return NotFound();

            if (offeringId <= 0)
            {
                TempData["ErrorMessage"] = "المقرر المطلوب غير صالح.";
                return RedirectToAction(nameof(Courses));
            }

            // نستخدم معاملة لحماية فحص المقاعد
            // عند وجود طلبات تسجيل متزامنة
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var offering = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .FirstOrDefaultAsync(o => o.Id == offeringId);

            if (offering == null)
            {
                TempData["ErrorMessage"] = "المقرر المطلوب غير موجود.";
                return RedirectToAction(nameof(Courses));
            }

            // ==========================================
            // التحقق من الفصل المعتمد للتسجيل
            // ==========================================
            var activeSemester = await _context.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            if (activeSemester == null ||
                offering.SemesterId != activeSemester.Id)
            {
                TempData["ErrorMessage"] =
                    "التسجيل متاح لمقررات الفصل الحالي فقط.";

                return RedirectToAction(nameof(Courses));
            }

            // ==========================================
            // التحقق من فترة التسجيل التي حددها الأدمن
            // ==========================================
            if (!IsRegistrationAvailable(offering.Semester))
            {
                TempData["ErrorMessage"] =
                    GetRegistrationMessage(offering.Semester);

                return RedirectToAction(nameof(Courses));
            }

            // ==========================================
            // التأكد من أن المقرر تابع لقسم الطالب
            // ==========================================
            if (offering.Course.DepartmentId != student.DepartmentId)
            {
                TempData["ErrorMessage"] =
                    "هذا المقرر غير تابع لقسمك الدراسي.";

                return RedirectToAction(nameof(Courses));
            }

            // ==========================================
            // التحقق من سعة الشعبة
            // ==========================================
            if (offering.Capacity <= 0)
            {
                TempData["ErrorMessage"] =
                    "لا توجد مقاعد متاحة في هذه الشعبة.";

                return RedirectToAction(nameof(Courses));
            }

            // ==========================================
            // منع تكرار تسجيل نفس المقرر
            // حتى لو كان له أكثر من شعبة
            // ==========================================
            bool alreadyRegistered = await _context.Registrations
                .AnyAsync(r =>
                    r.StudentId == student.Id &&
                    r.Status == "Registered" &&
                    r.CourseOffering.CourseId == offering.CourseId &&
                    r.CourseOffering.SemesterId == offering.SemesterId);

            if (alreadyRegistered)
            {
                TempData["ErrorMessage"] =
                    "أنت مسجل في هذا المقرر بالفعل.";

                return RedirectToAction(nameof(Courses));
            }

            int registeredCount = await _context.Registrations
                .CountAsync(r =>
                    r.CourseOfferingId == offering.Id &&
                    r.Status == "Registered");

            if (registeredCount >= offering.Capacity)
            {
                TempData["ErrorMessage"] =
                    "اكتملت المقاعد المتاحة لهذا المقرر.";

                return RedirectToAction(nameof(Courses));
            }

            // ==========================================
            // إعادة تفعيل التسجيل الملغى أو إنشاء تسجيل
            // ==========================================
            var cancelledRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r =>
                    r.StudentId == student.Id &&
                    r.CourseOfferingId == offering.Id &&
                    r.Status == "Cancelled");

            if (cancelledRegistration != null)
            {
                cancelledRegistration.Status = "Registered";
                cancelledRegistration.RegisteredAt = DateTime.UtcNow;
            }
            else
            {
                var registration = new Registration
                {
                    StudentId = student.Id,
                    CourseOfferingId = offering.Id,
                    RegisteredAt = DateTime.UtcNow,
                    Status = "Registered"
                };

                _context.Registrations.Add(registration);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "تم تسجيل المقرر بنجاح.";

            return RedirectToAction(nameof(Courses));
        }

        // ==========================================
        // عرض الرسوم الجامعية للطالب
        // GET: /StudentDashboard/Fees
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Fees()
        {
            var student = await GetCurrentStudentAsync();

            if (student == null)
                return NotFound();

            var activeSemester = await _context.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            ViewBag.ActiveSemester = activeSemester;

            var registrations = new List<Registration>();
            decimal? pricePerHour = null;
            decimal? estimatedFees = null;

            if (activeSemester != null)
            {
                registrations = await _context.Registrations
                    .AsNoTracking()
                    .Include(r => r.CourseOffering)
                        .ThenInclude(o => o.Course)
                    .Where(r =>
                        r.StudentId == student.Id &&
                        r.Status == "Registered" &&
                        r.CourseOffering.SemesterId == activeSemester.Id)
                    .ToListAsync();

                pricePerHour = await _context.HourPrices
                    .AsNoTracking()
                    .Where(h =>
                        h.DepartmentId == student.DepartmentId &&
                        h.SemesterId == activeSemester.Id)
                    .Select(h => (decimal?)h.PricePerHour)
                    .FirstOrDefaultAsync();

                if (pricePerHour.HasValue)
                {
                    int totalHours = registrations
                        .Sum(r =>
                            r.CourseOffering.Course.CreditHours);

                    estimatedFees = totalHours * pricePerHour.Value;
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