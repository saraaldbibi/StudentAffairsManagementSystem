using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class CoursesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoursesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // عرض جميع المقررات الدراسية مع أسعار الساعات
        // ==========================================
        public async Task<IActionResult> Index()
        {
            var courses = await _context.Courses
                .Include(c => c.Department)
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            // الفصل الدراسي الفعّال المعتمد
            var activeSemester = await _context.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            ViewBag.ActiveSemester = activeSemester;

            // ربط سعر الساعة بالقسم والفصل الفعّال
            var hourPrices = new Dictionary<long, decimal>();

            if (activeSemester != null)
            {
                var prices = await _context.HourPrices
                    .AsNoTracking()
                    .Where(h => h.SemesterId == activeSemester.Id)
                    .Select(h => new
                    {
                        h.DepartmentId,
                        h.PricePerHour
                    })
                    .ToListAsync();

                // في حال وجود سجلات مكررة قديمة،
                // نأخذ أول سعر لكل قسم دون تعطيل الصفحة.
                hourPrices = prices
                    .GroupBy(h => h.DepartmentId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.First().PricePerHour);
            }

            ViewBag.HourPrices = hourPrices;

            return View(courses);
        }

        // ==========================================
        // عرض تفاصيل مقرر دراسي
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var course = await _context.Courses
                .Include(c => c.Department)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();

            return View(course);
        }

        // ==========================================
        // فتح صفحة إضافة مقرر جديد
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadDepartments();

            return View(new Course());
        }

        // ==========================================
        // حفظ مقرر دراسي جديد
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,Code,CreditHours,DepartmentId")]
            Course course)
        {
            ModelState.Remove(nameof(Course.Department));

            course.Name = course.Name?.Trim() ?? "";
            course.Code = course.Code?.Trim() ?? "";

            await ValidateCourse(course);

            if (!ModelState.IsValid)
            {
                await LoadDepartments();
                return View(course);
            }

            _context.Courses.Add(course);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر حفظ المقرر. تحققوا من الرمز والقسم وحاولوا مجددًا.");

                await LoadDepartments();
                return View(course);
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // فتح صفحة تعديل مقرر
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var course = await _context.Courses
                .FindAsync(id);

            if (course == null)
                return NotFound();

            await LoadDepartments();

            return View(course);
        }

        // ==========================================
        // حفظ تعديلات المقرر
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind("Id,Name,Code,CreditHours,DepartmentId")]
            Course model)
        {
            if (id != model.Id)
                return NotFound();

            ModelState.Remove(nameof(Course.Department));

            model.Name = model.Name?.Trim() ?? "";
            model.Code = model.Code?.Trim() ?? "";

            await ValidateCourse(model, id);

            if (!ModelState.IsValid)
            {
                await LoadDepartments();
                return View(model);
            }

            var course = await _context.Courses
                .FindAsync(id);

            if (course == null)
                return NotFound();

            course.Name = model.Name;
            course.Code = model.Code;
            course.CreditHours = model.CreditHours;
            course.DepartmentId = model.DepartmentId;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر حفظ التعديلات. تحققوا من البيانات وحاولوا مجددًا.");

                await LoadDepartments();
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // فتح صفحة تأكيد حذف مقرر
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            var course = await _context.Courses
                .Include(c => c.Department)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();

            return View(course);
        }

        // ==========================================
        // تنفيذ حذف المقرر
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var course = await _context.Courses
                .FindAsync(id);

            if (course == null)
                return NotFound();

            // منع حذف مقرر مرتبط بطرح دراسي
            bool hasOfferings = await _context.CourseOfferings
                .AnyAsync(o => o.CourseId == id);

            if (hasOfferings)
            {
                TempData["Error"] =
                    "لا يمكن حذف المقرر لأنه مرتبط بمقررات مطروحة.";

                return RedirectToAction(nameof(Index));
            }

            _context.Courses.Remove(course);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "تعذر حذف المقرر لأنه قد يكون مرتبطًا ببيانات أخرى.";

                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // التحقق من صحة بيانات المقرر
        // ==========================================
        private async Task ValidateCourse(
            Course course,
            long? currentCourseId = null)
        {
            if (string.IsNullOrWhiteSpace(course.Name))
            {
                ModelState.AddModelError(
                    nameof(Course.Name),
                    "اسم المقرر مطلوب");
            }

            if (string.IsNullOrWhiteSpace(course.Code))
            {
                ModelState.AddModelError(
                    nameof(Course.Code),
                    "رمز المقرر مطلوب");
            }

            if (course.CreditHours <= 0)
            {
                ModelState.AddModelError(
                    nameof(Course.CreditHours),
                    "عدد الساعات لازم يكون أكبر من صفر");
            }

            bool departmentExists =
                await _context.Departments.AnyAsync(d =>
                    d.Id == course.DepartmentId &&
                    d.IsActive);

            if (!departmentExists)
            {
                ModelState.AddModelError(
                    nameof(Course.DepartmentId),
                    "القسم غير موجود أو غير فعّال");
            }

            // منع تكرار رمز المقرر ضمن نفس القسم
            if (!string.IsNullOrWhiteSpace(course.Code))
            {
                bool codeExists =
                    await _context.Courses.AnyAsync(c =>
                        c.Code == course.Code &&
                        c.DepartmentId == course.DepartmentId &&
                        (!currentCourseId.HasValue ||
                         c.Id != currentCourseId.Value));

                if (codeExists)
                {
                    ModelState.AddModelError(
                        nameof(Course.Code),
                        "رمز المقرر مستخدم مسبقًا في نفس القسم");
                }
            }
        }

        // ==========================================
        // تجهيز قائمة الأقسام الفعّالة
        // ==========================================
        private async Task LoadDepartments()
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();

            ViewBag.DepartmentId = new SelectList(
                departments,
                "Id",
                "Name");
        }
    }
}