using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class HourPricesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HourPricesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض جميع أسعار الساعات
        public async Task<IActionResult> Index()
        {
            var hourPrices = await _context.HourPrices
                .Include(h => h.Department)
                .Include(h => h.Semester)
                .AsNoTracking()
                .OrderBy(h => h.Department.Name)
                .ThenByDescending(h => h.Semester.StartDate)
                .ToListAsync();

            return View(hourPrices);
        }

        // عرض تفاصيل سعر ساعة
        public async Task<IActionResult> Details(long id)
        {
            var hourPrice = await _context.HourPrices
                .Include(h => h.Department)
                .Include(h => h.Semester)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == id);

            if (hourPrice == null)
                return NotFound();

            return View(hourPrice);
        }

        // فتح صفحة إضافة سعر ساعة
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadLists();

            return View(new HourPrice());
        }

        // حفظ سعر ساعة جديد
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("DepartmentId,SemesterId,PricePerHour")]
            HourPrice hourPrice)
        {
            // الخصائص المرتبطة لا تُرسل من النموذج
            ModelState.Remove(nameof(HourPrice.Department));
            ModelState.Remove(nameof(HourPrice.Semester));

            await ValidateHourPrice(hourPrice);

            if (!ModelState.IsValid)
            {
                await LoadLists();
                return View(hourPrice);
            }

            _context.HourPrices.Add(hourPrice);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر حفظ سعر الساعة. تحققوا من البيانات وحاولوا مجدداً.");

                await LoadLists();
                return View(hourPrice);
            }

            return RedirectToAction(nameof(Index));
        }

        // فتح صفحة تعديل سعر الساعة
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var hourPrice = await _context.HourPrices
                .FindAsync(id);

            if (hourPrice == null)
                return NotFound();

            await LoadLists();

            return View(hourPrice);
        }

        // حفظ التعديلات
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind("Id,DepartmentId,SemesterId,PricePerHour")]
            HourPrice model)
        {
            if (id != model.Id)
                return NotFound();

            ModelState.Remove(nameof(HourPrice.Department));
            ModelState.Remove(nameof(HourPrice.Semester));

            await ValidateHourPrice(model, id);

            if (!ModelState.IsValid)
            {
                await LoadLists();
                return View(model);
            }

            var hourPrice = await _context.HourPrices
                .FindAsync(id);

            if (hourPrice == null)
                return NotFound();

            hourPrice.DepartmentId = model.DepartmentId;
            hourPrice.SemesterId = model.SemesterId;
            hourPrice.PricePerHour = model.PricePerHour;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "تعذر حفظ التعديلات. تحققوا من البيانات وحاولوا مجدداً.");

                await LoadLists();
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        // فتح صفحة تأكيد الحذف
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            var hourPrice = await _context.HourPrices
                .Include(h => h.Department)
                .Include(h => h.Semester)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == id);

            if (hourPrice == null)
                return NotFound();

            return View(hourPrice);
        }

        // تنفيذ الحذف
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var hourPrice = await _context.HourPrices
                .FindAsync(id);

            if (hourPrice == null)
                return NotFound();

            _context.HourPrices.Remove(hourPrice);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "تعذر حذف سعر الساعة لأنه قد يكون مرتبطاً ببيانات أخرى.";

                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index));
        }

        // التحقق من صحة بيانات سعر الساعة
        private async Task ValidateHourPrice(
            HourPrice hourPrice,
            long? currentId = null)
        {
            // التأكد من وجود القسم
            bool departmentExists =
                await _context.Departments.AnyAsync(d =>
                    d.Id == hourPrice.DepartmentId);

            if (!departmentExists)
            {
                ModelState.AddModelError(
                    nameof(HourPrice.DepartmentId),
                    "القسم المختار غير موجود");
            }

            // التأكد من وجود الفصل الدراسي
            bool semesterExists =
                await _context.Semesters.AnyAsync(s =>
                    s.Id == hourPrice.SemesterId);

            if (!semesterExists)
            {
                ModelState.AddModelError(
                    nameof(HourPrice.SemesterId),
                    "الفصل الدراسي المختار غير موجود");
            }

            // السعر يجب أن يكون أكبر من صفر
            if (hourPrice.PricePerHour <= 0)
            {
                ModelState.AddModelError(
                    nameof(HourPrice.PricePerHour),
                    "سعر الساعة لازم يكون أكبر من صفر");
            }

            // منع تسجيل سعرين لنفس القسم والفصل
            bool duplicateExists =
                await _context.HourPrices.AnyAsync(h =>
                    h.DepartmentId == hourPrice.DepartmentId &&
                    h.SemesterId == hourPrice.SemesterId &&
                    (!currentId.HasValue ||
                     h.Id != currentId.Value));

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    nameof(HourPrice.SemesterId),
                    "يوجد سعر ساعة مسجل لهذا القسم في نفس الفصل");
            }
        }

        // تجهيز قوائم الأقسام والفصول
        private async Task LoadLists()
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .ToListAsync();

            var semesters = await _context.Semesters
                .AsNoTracking()
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            ViewBag.DepartmentId = new SelectList(
                departments,
                "Id",
                "Name");

            ViewBag.SemesterId = new SelectList(
                semesters,
                "Id",
                "Name");
        }
    }
}