
using System;

namespace StudentAffairsManagementSystem.Models
{
    public class Student
    {
        public long Id { get; set; }

        public string UniversityNumber { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string FatherName { get; set; } = string.Empty;

        public string MotherName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public long DepartmentId { get; set; }

        public long AccountId { get; set; }

        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string Governorate { get; set; } = string.Empty;

        public DateTime BirthDate { get; set; }

        public string Gender { get; set; } = string.Empty;

        public string NationalId { get; set; } = string.Empty;

        public long AdmissionSemesterId { get; set; }

        public string Status { get; set; } = string.Empty;

        public long? GraduationSemesterId { get; set; }

        public decimal? GraduationAverage { get; set; }

        public string Nationality { get; set; } = string.Empty;

        public string? PersonalImage { get; set; }

        public string? NationalIdImage { get; set; }

        public string? CertificateImage { get; set; }

        // ==========================================
        // الحذف الآمن للطالب
        // ==========================================

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        // ==========================================
        // العلاقات مع الجداول الأخرى
        // ==========================================

        public Department Department { get; set; } = null!;

        public Account Account { get; set; } = null!;

        public Semester AdmissionSemester { get; set; } = null!;

        public Semester? GraduationSemester { get; set; }
    }
}
