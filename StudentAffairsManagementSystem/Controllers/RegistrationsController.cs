using System;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Controllers
{
    // إدارة التسجيلات مسموحة للمدير والموظف فقط
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

        // عرض جميع التسجيلات
        public async Task<IActionResult> Index()
        {
            var registrations = await _context.Registrations
                .Include(r => r.Student)
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Course)
                .Include(r => r.CourseOffering)
                    .ThenInclude(o => o.Semester)
                .AsNoTracking()
                .OrderByDescending(r => r.RegisteredAt)
                .ToListAsync();

            return View(registrations);
        }

        // عرض تفاصيل تسجيل
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

        // فتح صفحة تسجيل طالب بمقرر
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadLists();

            return View(new Registration());
        }

        // تسجيل طالب بمقرر
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("StudentId,CourseOfferingId")]
            Registration model)
        {
            ModelState.Remove(nameof(Registration.Student));
            ModelState.Remove(nameof(Registration.CourseOffering));

            if (!ModelState.IsValid)
            {
                await LoadLists();
                return View(model);
            }

            // التأكد من وجود الطالب
            bool studentExists = await _context.Students
                .AnyAsync(s => s.Id == model.StudentId);

            if (!studentExists)
            {
                ModelState.AddModelError(
                    nameof(Registration.StudentId),
                    "الطالب غير موجود");
            }

            // التأكد من وجود المقرر المطروح
            bool offeringExists = await _context.CourseOfferings
                .AnyAsync(o => o.Id == model.CourseOfferingId);

            if (!offeringExists)
            {
                ModelState.AddModelError(
                    nameof(Registration.CourseOfferingId),
                    "المقرر المطروح غير موجود");
            }

            if (!ModelState.IsValid)
            {
                await LoadLists();
                return View(model);
            }

            // معاملة Serializable لحماية عملية التسجيل
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var offering = await _context.CourseOfferings
                .FirstOrDefaultAsync(o =>
                    o.Id == model.CourseOfferingId);

            if (offering == null)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    nameof(Registration.CourseOfferingId),
                    "المقرر المطروح غير موجود");

                await LoadLists();
                return View(model);
            }

            // البحث عن تسجيل سابق
            var existingRegistration =
                await _context.Registrations
                    .FirstOrDefaultAsync(r =>
                        r.StudentId == model.StudentId &&
                        r.CourseOfferingId == model.CourseOfferingId);

            if (existingRegistration != null &&
                existingRegistration.Status == Registered)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "الطالب مسجل مسبقاً بهذا المقرر");

                await LoadLists();
                return View(model);
            }

            // حساب عدد الطلاب المسجلين فعلياً
            int registeredCount =
                await _context.Registrations.CountAsync(r =>
                    r.CourseOfferingId == model.CourseOfferingId &&
                    r.Status == Registered);

            if (registeredCount >= offering.Capacity)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "لا توجد مقاعد شاغرة في هذا المقرر");

                await LoadLists();
                return View(model);
            }

            if (existingRegistration != null)
            {
                // إعادة تفعيل تسجيل ملغى
                if (existingRegistration.Status != Cancelled)
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        string.Empty,
                        "حالة التسجيل السابقة غير معروفة");

                    await LoadLists();
                    return View(model);
                }

                existingRegistration.Status = Registered;
                existingRegistration.RegisteredAt =
                    DateTime.UtcNow;
            }
            else
            {
                // إنشاء تسجيل جديد
                var registration = new Registration
                {
                    StudentId = model.StudentId,
                    CourseOfferingId = model.CourseOfferingId,
                    RegisteredAt = DateTime.UtcNow,
                    Status = Registered
                };

                _context.Registrations.Add(registration);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(nameof(Index));
        }

        // إلغاء تسجيل طالب
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(long id)
        {
            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
                return NotFound();

            if (registration.Status != Registered)
            {
                TempData["Error"] =
                    "هذا التسجيل غير فعال أو ملغى مسبقاً";

                return RedirectToAction(nameof(Index));
            }

            registration.Status = Cancelled;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // إعادة تفعيل تسجيل ملغى
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(long id)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
            {
                await transaction.RollbackAsync();
                return NotFound();
            }

            if (registration.Status != Cancelled)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "يمكن إعادة تفعيل التسجيلات الملغاة فقط";

                return RedirectToAction(nameof(Index));
            }

            var offering = await _context.CourseOfferings
                .FirstOrDefaultAsync(o =>
                    o.Id == registration.CourseOfferingId);

            if (offering == null)
            {
                await transaction.RollbackAsync();
                return NotFound();
            }

            int registeredCount =
                await _context.Registrations.CountAsync(r =>
                    r.CourseOfferingId ==
                        registration.CourseOfferingId &&
                    r.Status == Registered);

            if (registeredCount >= offering.Capacity)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "لا توجد مقاعد شاغرة لإعادة تفعيل التسجيل";

                return RedirectToAction(nameof(Index));
            }

            registration.Status = Registered;
            registration.RegisteredAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(nameof(Index));
        }

        // حذف تسجيل ملغى نهائياً
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
                return NotFound();

            if (registration.Status != Cancelled)
            {
                TempData["Error"] =
                    "يجب إلغاء التسجيل قبل حذفه";

                return RedirectToAction(nameof(Index));
            }

            _context.Registrations.Remove(registration);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // تجهيز قوائم الطلاب والمقررات المطروحة
        private async Task LoadLists()
        {
            var students = await _context.Students
                .AsNoTracking()
                .OrderBy(s => s.UniversityNumber)
                .ToListAsync();

            var offerings = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .AsNoTracking()
                .OrderBy(o => o.Course.Name)
                .ToListAsync();

            ViewBag.StudentId = new SelectList(
                students,
                "Id",
                "UniversityNumber");

            ViewBag.CourseOfferingId = new SelectList(
                offerings,
                "Id",
                "Id");
        }
    }
}