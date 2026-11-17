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

        // عرض جميع المقررات المطروحة
        public async Task<IActionResult> Index()
        {
            var offerings = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .Include(o => o.Employee)
                .AsNoTracking()
                .OrderByDescending(o => o.Semester.StartDate)
                .ThenBy(o => o.Course.Name)
                .ToListAsync();

            return View(offerings);
        }

        // عرض تفاصيل مقرر مطروح
        public async Task<IActionResult> Details(long id)
        {
            var offering = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .Include(o => o.Employee)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offering == null)
                return NotFound();

            return View(offering);
        }

        // فتح صفحة إضافة طرح جديد
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadLists();

            return View(new CourseOffering());
        }

        // حفظ طرح مقرر جديد
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("CourseId,SemesterId,EmployeeId,Capacity")]
            CourseOffering offering)
        {
            // الخصائص المرتبطة لا تأتي من النموذج
            ModelState.Remove(nameof(CourseOffering.Course));
            ModelState.Remove(nameof(CourseOffering.Semester));
            ModelState.Remove(nameof(CourseOffering.Employee));

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
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر حفظ الطرح. تحققوا من البيانات " +
                    "ومن عدم تكرار المقرر في نفس الفصل.");

                await LoadLists();
                return View(offering);
            }

            return RedirectToAction(nameof(Index));
        }

        // فتح صفحة تعديل طرح
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

        // حفظ التعديلات
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind("Id,CourseId,SemesterId,EmployeeId,Capacity")]
            CourseOffering model)
        {
            if (id != model.Id)
                return NotFound();

            ModelState.Remove(nameof(CourseOffering.Course));
            ModelState.Remove(nameof(CourseOffering.Semester));
            ModelState.Remove(nameof(CourseOffering.Employee));

            var offering = await _context.CourseOfferings
                .FindAsync(id);

            if (offering == null)
                return NotFound();

            // منع تغيير المقرر أو الفصل إذا كان الطرح مرتبطاً بتسجيلات
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
            offering.EmployeeId = model.EmployeeId;
            offering.Capacity = model.Capacity;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر حفظ التعديلات. تحققوا من البيانات " +
                    "وحاولوا مجدداً.");

                await LoadLists();
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        // فتح صفحة تأكيد حذف طرح
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            var offering = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Semester)
                .Include(o => o.Employee)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offering == null)
                return NotFound();

            return View(offering);
        }

        // تنفيذ حذف الطرح
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var offering = await _context.CourseOfferings
                .FindAsync(id);

            if (offering == null)
                return NotFound();

            // منع الحذف إذا كان الطرح مرتبطاً بتسجيلات طلاب
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
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "تعذر حذف المقرر المطروح لأنه قد يكون " +
                    "مرتبطاً ببيانات أخرى.";

                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index));
        }

        // التحقق من بيانات الطرح
        private async Task ValidateOffering(
            CourseOffering offering,
            long? currentId = null)
        {
            // التأكد من وجود المقرر
            bool courseExists =
                await _context.Courses.AnyAsync(c =>
                    c.Id == offering.CourseId);

            if (!courseExists)
            {
                ModelState.AddModelError(
                    nameof(CourseOffering.CourseId),
                    "المقرر المختار غير موجود");
            }

            // التأكد من وجود الفصل الدراسي
            bool semesterExists =
                await _context.Semesters.AnyAsync(s =>
                    s.Id == offering.SemesterId);

            if (!semesterExists)
            {
                ModelState.AddModelError(
                    nameof(CourseOffering.SemesterId),
                    "الفصل الدراسي المختار غير موجود");
            }

            // التأكد من وجود الموظف
            bool employeeExists =
                await _context.Employees.AnyAsync(e =>
                    e.Id == offering.EmployeeId);

            if (!employeeExists)
            {
                ModelState.AddModelError(
                    nameof(CourseOffering.EmployeeId),
                    "الموظف المختار غير موجود");
            }

            // التأكد من أن السعة أكبر من صفر
            if (offering.Capacity <= 0)
            {
                ModelState.AddModelError(
                    nameof(CourseOffering.Capacity),
                    "سعة المقرر لازم تكون أكبر من صفر");
            }

            // منع طرح نفس المقرر مرتين في نفس الفصل
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
                    "هذا المقرر مطروح مسبقاً في نفس الفصل");
            }

            // منع تقليل السعة تحت عدد التسجيلات الفعالة
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
                        "السعة لا يمكن أن تكون أقل من " +
                        "عدد الطلاب المسجلين حالياً");
                }
            }
        }

        // تجهيز قوائم المقررات والفصول والموظفين
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

            var employees = await _context.Employees
                .AsNoTracking()
                .OrderBy(e => e.FirstName)
                .ToListAsync();

            ViewBag.CourseId = new SelectList(
                courses, "Id", "Name");

            ViewBag.SemesterId = new SelectList(
                semesters, "Id", "Name");

            ViewBag.EmployeeId = new SelectList(
                employees, "Id", "FirstName");
        }
    }
}