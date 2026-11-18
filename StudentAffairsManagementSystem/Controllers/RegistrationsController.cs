
using System;
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

        public RegistrationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض جميع التسجيلات، بما فيها التسجيلات الملغاة
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

        // تسجيل طالب بمقرر ضمن فصل فعال
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("StudentId,CourseOfferingId")]
            Registration model)
        {
            // الخصائص المرتبطة لا تأتي من النموذج
            ModelState.Remove(nameof(Registration.Student));
            ModelState.Remove(nameof(Registration.CourseOffering));

            // التاريخ والحالة يحددهما السيرفر
            ModelState.Remove(nameof(Registration.RegisteredAt));
            ModelState.Remove(nameof(Registration.Status));

            if (!ModelState.IsValid)
            {
                await LoadLists();
                return View(model);
            }

            // التحقق من الطالب
            bool studentExists = await _context.Students
                .AnyAsync(s =>
                    s.Id == model.StudentId &&
                    !s.IsDeleted);

            if (!studentExists)
            {
                ModelState.AddModelError(
                    nameof(Registration.StudentId),
                    "الطالب غير موجود أو محذوف من النظام.");
            }

            // التحقق من وجود الطرح ضمن فصل فعال
            bool offeringExists = await _context.CourseOfferings
                .AnyAsync(o =>
                    o.Id == model.CourseOfferingId &&
                    o.Semester.IsActive);

            if (!offeringExists)
            {
                ModelState.AddModelError(
                    nameof(Registration.CourseOfferingId),
                    "المقرر المطروح غير موجود أو أن فصله الدراسي غير فعال.");
            }

            if (!ModelState.IsValid)
            {
                await LoadLists();
                return View(model);
            }

            try
            {
                // حماية فحص السعة والتسجيل من العمليات المتزامنة
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                // إعادة التحقق داخل المعاملة
                bool validStudent = await _context.Students
                    .AnyAsync(s =>
                        s.Id == model.StudentId &&
                        !s.IsDeleted);

                if (!validStudent)
                {
                    ModelState.AddModelError(
                        nameof(Registration.StudentId),
                        "الطالب غير موجود أو محذوف من النظام.");

                    await transaction.RollbackAsync();
                    await LoadLists();
                    return View(model);
                }

                var offering = await _context.CourseOfferings
                    .Include(o => o.Semester)
                    .FirstOrDefaultAsync(o =>
                        o.Id == model.CourseOfferingId);

                if (offering == null || !offering.Semester.IsActive)
                {
                    ModelState.AddModelError(
                        nameof(Registration.CourseOfferingId),
                        "التسجيل متاح فقط في المقررات المطروحة ضمن فصل فعال.");

                    await transaction.RollbackAsync();
                    await LoadLists();
                    return View(model);
                }

                // البحث عن تسجيل سابق لنفس الطالب والطرح
                var existingRegistration =
                    await _context.Registrations
                        .FirstOrDefaultAsync(r =>
                            r.StudentId == model.StudentId &&
                            r.CourseOfferingId == model.CourseOfferingId);

                if (existingRegistration != null &&
                    existingRegistration.Status == Registered)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "الطالب مسجل مسبقًا بهذا المقرر المطروح.");

                    await transaction.RollbackAsync();
                    await LoadLists();
                    return View(model);
                }

                // رفض الحالات غير المعروفة
                if (existingRegistration != null &&
                    existingRegistration.Status != Cancelled)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "حالة التسجيل السابقة غير معروفة.");

                    await transaction.RollbackAsync();
                    await LoadLists();
                    return View(model);
                }

                // حساب التسجيلات الفعالة فقط
                int registeredCount =
                    await _context.Registrations.CountAsync(r =>
                        r.CourseOfferingId == model.CourseOfferingId &&
                        r.Status == Registered);

                if (registeredCount >= offering.Capacity)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "لا توجد مقاعد شاغرة في هذا المقرر المطروح.");

                    await transaction.RollbackAsync();
                    await LoadLists();
                    return View(model);
                }

                if (existingRegistration != null)
                {
                    // إعادة تفعيل تسجيل ملغى
                    existingRegistration.Status = Registered;
                    existingRegistration.RegisteredAt = DateTime.UtcNow;
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

                TempData["Success"] =
                    "تم تسجيل الطالب في المقرر بنجاح.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر حفظ التسجيل بسبب تعارض في البيانات. يرجى المحاولة مجددًا.");
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is Microsoft.Data.SqlClient.SqlException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر إتمام التسجيل حاليًا بسبب تعارض أو مشكلة في قاعدة البيانات. يرجى المحاولة مجددًا.");
            }

            await LoadLists();
            return View(model);
        }

        // إلغاء تسجيل طالب مع الاحتفاظ بالسجل
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
                    "هذا التسجيل غير فعال أو ملغى مسبقًا.";

                return RedirectToAction(nameof(Index));
            }

            // الإلغاء لا يحذف السجل الأكاديمي
            registration.Status = Cancelled;

            try
            {
                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "تم إلغاء تسجيل الطالب مع الاحتفاظ بالسجل.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "تعذر إلغاء التسجيل. يرجى المحاولة مجددًا.";
            }

            return RedirectToAction(nameof(Index));
        }

        // إعادة تفعيل تسجيل ملغى ضمن فصل فعال
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(long id)
        {
            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var registration = await _context.Registrations
                    .Include(r => r.Student)
                    .Include(r => r.CourseOffering)
                        .ThenInclude(o => o.Semester)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (registration == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                if (registration.Status != Cancelled)
                {
                    TempData["Error"] =
                        "يمكن إعادة تفعيل التسجيلات الملغاة فقط.";

                    await transaction.RollbackAsync();
                    return RedirectToAction(nameof(Index));
                }

                // منع إعادة تفعيل تسجيل طالب محذوف
                if (registration.Student.IsDeleted)
                {
                    TempData["Error"] =
                        "لا يمكن إعادة تفعيل تسجيل طالب محذوف من النظام.";

                    await transaction.RollbackAsync();
                    return RedirectToAction(nameof(Index));
                }

                var offering = registration.CourseOffering;

                // السماح بإعادة التفعيل فقط في فصل فعال
                if (!offering.Semester.IsActive)
                {
                    TempData["Error"] =
                        "لا يمكن إعادة تفعيل التسجيل لأن الفصل الدراسي غير فعال.";

                    await transaction.RollbackAsync();
                    return RedirectToAction(nameof(Index));
                }

                int registeredCount =
                    await _context.Registrations.CountAsync(r =>
                        r.CourseOfferingId ==
                            registration.CourseOfferingId &&
                        r.Status == Registered);

                if (registeredCount >= offering.Capacity)
                {
                    TempData["Error"] =
                        "لا توجد مقاعد شاغرة لإعادة تفعيل التسجيل.";

                    await transaction.RollbackAsync();
                    return RedirectToAction(nameof(Index));
                }

                registration.Status = Registered;
                registration.RegisteredAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    "تمت إعادة تفعيل تسجيل الطالب بنجاح.";
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
                    "تعذرت إعادة التفعيل حاليًا. يرجى المحاولة مجددًا.";
            }

            return RedirectToAction(nameof(Index));
        }

        // تجهيز قوائم الطلاب والمقررات المطروحة
        private async Task LoadLists()
        {
            // الطلاب غير المحذوفين فقط
            var students = await _context.Students
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.UniversityNumber)
                .ToListAsync();

            // المقررات المطروحة ضمن الفصول الفعالة فقط
            var offerings = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .AsNoTracking()
                .Where(o => o.Semester.IsActive)
                .OrderBy(o => o.Course.Name)
                .ThenBy(o => o.Semester.StartDate)
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
