using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class SemestersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SemestersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض جميع الفصول الدراسية
        public async Task<IActionResult> Index()
        {
            var semesters = await _context.Semesters
                .AsNoTracking()
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            return View(semesters);
        }

        // عرض تفاصيل فصل دراسي
        public async Task<IActionResult> Details(long id)
        {
            var semester = await _context.Semesters
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (semester == null)
                return NotFound();

            return View(semester);
        }

        // فتح صفحة إضافة فصل
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new Semester
            {
                IsRegistrationOpen = false
            });
        }

        // حفظ فصل دراسي جديد
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind(
                "SemesterNumber,Name,AcademicYear," +
                "StartDate,EndDate,IsActive," +
                "RegistrationStartDate,RegistrationEndDate," +
                "IsRegistrationOpen")]
            Semester semester)
        {
            NormalizeSemester(semester);

            ValidateSemester(semester);

            await ValidateUniqueSemester(
                semester,
                excludedId: null);

            if (!ModelState.IsValid)
                return View(semester);

            _context.Semesters.Add(semester);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "تمت إضافة الفصل الدراسي بنجاح.";

            return RedirectToAction(nameof(Index));
        }

        // فتح صفحة تعديل فصل
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var semester = await _context.Semesters
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (semester == null)
                return NotFound();

            return View(semester);
        }

        // حفظ تعديلات الفصل
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind(
                "Id,SemesterNumber,Name,AcademicYear," +
                "StartDate,EndDate,IsActive," +
                "RegistrationStartDate,RegistrationEndDate," +
                "IsRegistrationOpen")]
            Semester model)
        {
            if (id != model.Id)
                return NotFound();

            NormalizeSemester(model);

            ValidateSemester(model);

            await ValidateUniqueSemester(
                model,
                excludedId: id);

            if (!ModelState.IsValid)
                return View(model);

            var semester = await _context.Semesters
                .FindAsync(id);

            if (semester == null)
                return NotFound();

            semester.SemesterNumber = model.SemesterNumber;
            semester.Name = model.Name;
            semester.AcademicYear = model.AcademicYear;
            semester.StartDate = model.StartDate;
            semester.EndDate = model.EndDate;
            semester.IsActive = model.IsActive;

            // إعدادات تسجيل المقررات
            semester.RegistrationStartDate =
                model.RegistrationStartDate;

            semester.RegistrationEndDate =
                model.RegistrationEndDate;

            semester.IsRegistrationOpen =
                model.IsRegistrationOpen;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "تم تعديل الفصل وإعدادات التسجيل بنجاح.";

            return RedirectToAction(nameof(Index));
        }

        // فتح صفحة تأكيد الحذف
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            var semester = await _context.Semesters
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (semester == null)
                return NotFound();

            return View(semester);
        }

        // تنفيذ حذف الفصل
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var semester = await _context.Semesters
                .FindAsync(id);

            if (semester == null)
                return NotFound();

            bool hasAdmissionStudents =
                await _context.Students.AnyAsync(s =>
                    s.AdmissionSemesterId == id);

            bool hasGraduationStudents =
                await _context.Students.AnyAsync(s =>
                    s.GraduationSemesterId == id);

            bool hasOfferings =
                await _context.CourseOfferings.AnyAsync(o =>
                    o.SemesterId == id);

            bool hasHourPrices =
                await _context.HourPrices.AnyAsync(h =>
                    h.SemesterId == id);

            if (hasAdmissionStudents ||
                hasGraduationStudents ||
                hasOfferings ||
                hasHourPrices)
            {
                TempData["Error"] =
                    "لا يمكن حذف الفصل لأنه مرتبط بطلاب " +
                    "أو مقررات مطروحة أو أسعار ساعات. " +
                    "يمكن تعطيله بدلاً من حذفه.";

                return RedirectToAction(nameof(Index));
            }

            _context.Semesters.Remove(semester);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "تعذر حذف الفصل لأنه قد يكون مرتبطاً " +
                    "ببيانات أخرى.";

                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index));
        }

        // إزالة المسافات الزائدة
        private static void NormalizeSemester(Semester semester)
        {
            semester.Name = semester.Name?.Trim() ?? "";

            semester.AcademicYear =
                semester.AcademicYear?.Trim() ?? "";
        }

        // التحقق من صحة بيانات الفصل وفترة التسجيل
        private void ValidateSemester(Semester semester)
        {
            if (semester.SemesterNumber <= 0)
            {
                ModelState.AddModelError(
                    nameof(Semester.SemesterNumber),
                    "رقم الفصل لازم يكون أكبر من صفر");
            }

            if (string.IsNullOrWhiteSpace(semester.Name))
            {
                ModelState.AddModelError(
                    nameof(Semester.Name),
                    "اسم الفصل مطلوب");
            }

            if (string.IsNullOrWhiteSpace(semester.AcademicYear))
            {
                ModelState.AddModelError(
                    nameof(Semester.AcademicYear),
                    "السنة الدراسية مطلوبة");
            }

            if (semester.StartDate == default)
            {
                ModelState.AddModelError(
                    nameof(Semester.StartDate),
                    "تاريخ بداية الفصل مطلوب");
            }

            if (semester.EndDate == default)
            {
                ModelState.AddModelError(
                    nameof(Semester.EndDate),
                    "تاريخ نهاية الفصل مطلوب");
            }

            if (semester.StartDate != default &&
                semester.EndDate != default &&
                semester.EndDate <= semester.StartDate)
            {
                ModelState.AddModelError(
                    nameof(Semester.EndDate),
                    "تاريخ نهاية الفصل لازم يكون بعد تاريخ البداية");
            }

            // إذا تم تحديد تاريخ واحد، يجب تحديد الآخر
            if (semester.RegistrationStartDate.HasValue !=
                semester.RegistrationEndDate.HasValue)
            {
                ModelState.AddModelError(
                    nameof(Semester.RegistrationEndDate),
                    "يجب تحديد تاريخ بداية التسجيل ونهايته معاً");
            }

            // التحقق من ترتيب فترة التسجيل
            if (semester.RegistrationStartDate.HasValue &&
                semester.RegistrationEndDate.HasValue &&
                semester.RegistrationEndDate.Value <=
                semester.RegistrationStartDate.Value)
            {
                ModelState.AddModelError(
                    nameof(Semester.RegistrationEndDate),
                    "نهاية فترة التسجيل يجب أن تكون بعد بدايتها");
            }

            // منع فتح التسجيل لفصل غير فعال
            if (semester.IsRegistrationOpen && !semester.IsActive)
            {
                ModelState.AddModelError(
                    nameof(Semester.IsRegistrationOpen),
                    "لا يمكن فتح التسجيل لفصل دراسي غير فعّال");
            }

            // منع فتح التسجيل دون تحديد الفترة
            if (semester.IsRegistrationOpen &&
                (!semester.RegistrationStartDate.HasValue ||
                 !semester.RegistrationEndDate.HasValue))
            {
                ModelState.AddModelError(
                    nameof(Semester.IsRegistrationOpen),
                    "يجب تحديد بداية ونهاية فترة التسجيل قبل فتحه");
            }
        }

        // منع تكرار رقم الفصل ضمن السنة الدراسية نفسها
        private async Task ValidateUniqueSemester(
            Semester semester,
            long? excludedId)
        {
            if (semester.SemesterNumber <= 0 ||
                string.IsNullOrWhiteSpace(semester.AcademicYear))
            {
                return;
            }

            bool exists = await _context.Semesters
                .AnyAsync(s =>
                    s.SemesterNumber == semester.SemesterNumber &&
                    s.AcademicYear == semester.AcademicYear &&
                    (!excludedId.HasValue ||
                     s.Id != excludedId.Value));

            if (exists)
            {
                ModelState.AddModelError(
                    nameof(Semester.SemesterNumber),
                    "يوجد فصل بنفس الرقم ضمن السنة الدراسية نفسها");
            }
        }
    }
}