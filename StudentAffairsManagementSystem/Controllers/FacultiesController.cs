using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class FacultiesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FacultiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض جميع الكليات
        public async Task<IActionResult> Index()
        {
            var faculties = await _context.Faculties
                .AsNoTracking()
                .OrderBy(f => f.Name)
                .ToListAsync();

            return View(faculties);
        }

        // عرض تفاصيل الكلية
        public async Task<IActionResult> Details(long id)
        {
            var faculty = await _context.Faculties
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id);

            if (faculty == null)
                return NotFound();

            return View(faculty);
        }

        // فتح صفحة إضافة كلية
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new Faculty());
        }

        // حفظ كلية جديدة
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,IsActive")] Faculty faculty)
        {
            if (string.IsNullOrWhiteSpace(faculty.Name))
            {
                ModelState.AddModelError(
                    nameof(Faculty.Name),
                    "اسم الكلية مطلوب");
            }

            if (!ModelState.IsValid)
                return View(faculty);

            faculty.Name = faculty.Name.Trim();

            _context.Faculties.Add(faculty);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // فتح صفحة تعديل كلية
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var faculty = await _context.Faculties
                .FindAsync(id);

            if (faculty == null)
                return NotFound();

            return View(faculty);
        }

        // حفظ تعديل الكلية
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind("Id,Name,IsActive")] Faculty model)
        {
            if (id != model.Id)
                return NotFound();

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError(
                    nameof(Faculty.Name),
                    "اسم الكلية مطلوب");
            }

            if (!ModelState.IsValid)
                return View(model);

            var faculty = await _context.Faculties
                .FindAsync(id);

            if (faculty == null)
                return NotFound();

            faculty.Name = model.Name.Trim();
            faculty.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // فتح صفحة تأكيد الحذف
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            var faculty = await _context.Faculties
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id);

            if (faculty == null)
                return NotFound();

            return View(faculty);
        }

        // تنفيذ حذف الكلية
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var faculty = await _context.Faculties
                .FindAsync(id);

            if (faculty == null)
                return NotFound();

            // منع حذف الكلية إذا كانت مرتبطة بأقسام
            bool hasDepartments =
                await _context.Departments.AnyAsync(d =>
                    d.FacultyId == id);

            if (hasDepartments)
            {
                TempData["Error"] =
                    "لا يمكن حذف الكلية لأنها مرتبطة بأقسام. " +
                    "يمكن تعطيل الكلية بدلاً من حذفها.";

                return RedirectToAction(nameof(Index));
            }

            _context.Faculties.Remove(faculty);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}