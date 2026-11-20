
using System;
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
    public class CourseOfferingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CourseOfferingsController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // عرض جميع المقررات المطروحة
        // GET: /CourseOfferings
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var offerings = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .AsNoTracking()
                .OrderByDescending(o => o.Semester.StartDate)
                .ThenBy(o => o.Course.Name)
                .ToListAsync();

            return View(offerings);
        }

        // ==========================================
        // عرض تفاصيل مقرر مطروح
        // GET: /CourseOfferings/Details/5
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var offering = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offering == null)
                return NotFound();

            return View(offering);
        }

        // ==========================================
        // فتح صفحة طرح مقرر جديد
        // GET: /CourseOfferings/Create
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadLists();

            return View(new CourseOffering());
        }

        // ==========================================
        // حفظ مقرر مطروح جديد
        // POST: /CourseOfferings/Create
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("CourseId,SemesterId,Capacity")]
            CourseOffering offering)
        {
            // الخصائص المرتبطة لا تُرسل من النموذج
            ModelState.Remove(nameof(CourseOffering.Course));
            ModelState.Remove(nameof(CourseOffering.Semester));

            await ValidateOffering(offering);

            if (!ModelState.IsValid)
            {
                await LoadLists();
                return View(offering);
            }

            _context.CourseOfferings.Add(offering);

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "تم طرح المقرر الدراسي بنجاح.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر حفظ المقرر المطروح. يرجى التحقق " +
                    "من البيانات وعدم تكرار المقرر في الفصل نفسه.");

                await LoadLists();
                return View(offering);
            }
        }

        // ==========================================
        // فتح صفحة تعديل مقرر مطروح
        // GET: /CourseOfferings/Edit/5
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var offering = await _context.CourseOfferings
                .FindAsync(id);

            if (offering == null)
                return NotFound();

            await LoadLists();

            return View(offering);
        }

        // ==========================================
        // حفظ تعديلات المقرر المطروح
        // POST: /CourseOfferings/Edit/5
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind("Id,CourseId,SemesterId,Capacity")]
            CourseOffering model)
        {
            if (id != model.Id)
                return NotFound();

            ModelState.Remove(nameof(CourseOffering.Course));
            ModelState.Remove(nameof(CourseOffering.Semester));

            var offering = await _context.CourseOfferings
                .FindAsync(id);

            if (offering == null)
                return NotFound();

            // لا نسمح بتغيير المقرر أو الفصل
            // بعد وجود تسجيلات مرتبطة بالطرح
            bool hasRegistrations =
                await _context.Registrations.AnyAsync(r =>
                    r.CourseOfferingId == id);

            if (hasRegistrations &&
                (offering.CourseId != model.CourseId ||
                 offering.SemesterId != model.SemesterId))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "لا يمكن تغيير المقرر أو الفصل الدراسي " +
                    "بعد وجود تسجيلات طلاب لهذا الطرح.");
            }

            await ValidateOffering(model, id);

            if (!ModelState.IsValid)
            {
                await LoadLists();
                return View(model);
            }

            offering.CourseId = model.CourseId;
            offering.SemesterId = model.SemesterId;
            offering.Capacity = model.Capacity;

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "تم تعديل المقرر المطروح بنجاح.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر حفظ التعديلات. يرجى التحقق " +
                    "من البيانات والمحاولة مجددًا.");

                await LoadLists();
                return View(model);
            }
        }

        // ==========================================
        // صفحة تأكيد حذف مقرر مطروح
        // GET: /CourseOfferings/Delete/5
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            var offering = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offering == null)
                return NotFound();

            return View(offering);
        }

        // ==========================================
        // تنفيذ حذف مقرر مطروح
        // POST: /CourseOfferings/Delete/5
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var offering = await _context.CourseOfferings
                .FindAsync(id);

            if (offering == null)
                return NotFound();

            // حماية تسجيلات الطلاب
            bool hasRegistrations =
                await _context.Registrations.AnyAsync(r =>
                    r.CourseOfferingId == id);

            if (hasRegistrations)
            {
                TempData["Error"] =
                    "لا يمكن حذف المقرر المطروح لأنه " +
                    "مرتبط بتسجيلات طلاب.";

                return RedirectToAction(nameof(Index));
            }

            _context.CourseOfferings.Remove(offering);

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "تم حذف المقرر المطروح بنجاح.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "تعذر حذف المقرر المطروح لأنه قد يكون " +
                    "مرتبطًا ببيانات أخرى.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // التحقق من بيانات المقرر المطروح
        // ==========================================
        private async Task ValidateOffering(
            CourseOffering offering,
            long? currentId = null)
        {
            // التأكد من وجود المقرر الدراسي
            bool courseExists =
                await _context.Courses.AnyAsync(c =>
                    c.Id == offering.CourseId);

            if (!courseExists)
            {
                ModelState.AddModelError(
                    nameof(CourseOffering.CourseId),
                    "المقرر الدراسي المختار غير موجود.");
            }

            // التأكد من وجود الفصل الدراسي
            bool semesterExists =
                await _context.Semesters.AnyAsync(s =>
                    s.Id == offering.SemesterId);

            if (!semesterExists)
            {
                ModelState.AddModelError(
                    nameof(CourseOffering.SemesterId),
                    "الفصل الدراسي المختار غير موجود.");
            }

            // يجب أن تكون السعة أكبر من صفر
            if (offering.Capacity <= 0)
            {
                ModelState.AddModelError(
                    nameof(CourseOffering.Capacity),
                    "يجب أن تكون السعة الاستيعابية أكبر من صفر.");
            }

            // منع طرح المقرر نفسه مرتين في الفصل نفسه
            bool duplicateExists =
                await _context.CourseOfferings.AnyAsync(o =>
                    o.CourseId == offering.CourseId &&
                    o.SemesterId == offering.SemesterId &&
                    (!currentId.HasValue ||
                     o.Id != currentId.Value));

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    nameof(CourseOffering.CourseId),
                    "هذا المقرر مطروح مسبقًا في الفصل الدراسي نفسه.");
            }

            // منع تخفيض السعة إلى أقل من عدد المسجلين
            if (currentId.HasValue)
            {
                int registeredCount =
                    await _context.Registrations.CountAsync(r =>
                        r.CourseOfferingId == currentId.Value &&
                        r.Status == "Registered");

                if (offering.Capacity < registeredCount)
                {
                    ModelState.AddModelError(
                        nameof(CourseOffering.Capacity),
                        "لا يمكن أن تقل السعة الاستيعابية " +
                        "عن عدد الطلاب المسجلين حاليًا.");
                }
            }
        }

        // ==========================================
        // تجهيز قوائم المقررات والفصول
        // ==========================================
        private async Task LoadLists()
        {
            var courses = await _context.Courses
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            var semesters = await _context.Semesters
                .AsNoTracking()
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            ViewBag.CourseId = new SelectList(
                courses, "Id", "Name");

            ViewBag.SemesterId = new SelectList(
                semesters, "Id", "Name");
        }
    }
}
