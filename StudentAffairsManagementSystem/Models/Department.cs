using System.ComponentModel.DataAnnotations;

namespace StudentAffairsManagementSystem.Models
{
    public class Department
    {
        public long Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // رمز الفرع مثل 11 لهندسة المعلوماتية
        [Required]
        [RegularExpression(@"^[0-9]{2}$",
            ErrorMessage = "رمز الفرع لازم يكون رقمين بالضبط")]
        public string Code { get; set; } = string.Empty;

        // رقم الكلية التابعة إلها القسم
        public long FacultyId { get; set; }

        // العلاقة مع جدول الكليات
        public Faculty Faculty { get; set; } = null!;

        // حالة القسم: فعال أو غير فعال
        public bool IsActive { get; set; }
    }
}