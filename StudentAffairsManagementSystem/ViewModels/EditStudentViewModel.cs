
using System;
using System.ComponentModel.DataAnnotations;

namespace StudentAffairsManagementSystem.ViewModels
{
    public class EditStudentViewModel
    {
        public long Id { get; set; }

        // للعرض فقط، وليس لتغيير الرقم الجامعي
        public string UniversityNumber { get; set; } = string.Empty;

        // البيانات الشخصية

        [Required(ErrorMessage = "اسم الطالب مطلوب")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم الأب مطلوب")]
        public string FatherName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم الأم مطلوب")]
        public string MotherName { get; set; } = string.Empty;

        [Required(ErrorMessage = "الكنية مطلوبة")]
        public string LastName { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime BirthDate { get; set; }

        public string Gender { get; set; } = string.Empty;

        public string NationalId { get; set; } = string.Empty;

        public string Nationality { get; set; } = string.Empty;

        // البيانات الأكاديمية

        [Range(1, long.MaxValue,
            ErrorMessage = "يرجى اختيار القسم الأكاديمي")]
        public long DepartmentId { get; set; }

        [Range(1, long.MaxValue,
            ErrorMessage = "يرجى اختيار فصل القبول")]
        public long AdmissionSemesterId { get; set; }

        public long? GraduationSemesterId { get; set; }

        public string Status { get; set; } = string.Empty;

        public decimal? GraduationAverage { get; set; }

        // معلومات التواصل

        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string Governorate { get; set; } = string.Empty;

        // مسارات الصور والوثائق

        public string? PersonalImage { get; set; }

        public string? NationalIdImage { get; set; }

        public string? CertificateImage { get; set; }
    }
}
