using System;
using System.ComponentModel.DataAnnotations;

namespace StudentAffairsManagementSystem.Models
{
    public class Semester
    {
        public long Id { get; set; }

        public int SemesterNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        public string AcademicYear { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; }

        // ==========================================
        // إعدادات فترة تسجيل المقررات
        // يتحكم بها الأدمن فقط
        // ==========================================

        [Display(Name = "بداية فترة التسجيل")]
        public DateTime? RegistrationStartDate { get; set; }

        [Display(Name = "نهاية فترة التسجيل")]
        public DateTime? RegistrationEndDate { get; set; }

        [Display(Name = "فتح تسجيل المقررات")]
        public bool IsRegistrationOpen { get; set; } = false;
    }
}