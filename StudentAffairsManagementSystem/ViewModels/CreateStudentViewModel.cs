using System;
using System.ComponentModel.DataAnnotations;

namespace StudentAffairsManagementSystem.ViewModels
{
    public class CreateStudentViewModel
    {
        // معلومات الطالب الأساسية

        [Required(ErrorMessage = "اسم الطالب مطلوب")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم الأب مطلوب")]
        public string FatherName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم الأم مطلوب")]
        public string MotherName { get; set; } = string.Empty;

        [Required(ErrorMessage = "الكنية مطلوبة")]
        public string LastName { get; set; } = string.Empty;

        // القسم وفصل القبول

        [Required(ErrorMessage = "القسم مطلوب")]
        public long DepartmentId { get; set; }

        [Required(ErrorMessage = "فصل القبول مطلوب")]
        public long AdmissionSemesterId { get; set; }

        // فصل التخرج اختياري
        public long? GraduationSemesterId { get; set; }

        // معلومات التواصل

        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string Governorate { get; set; } = string.Empty;

        // المعلومات الشخصية

        [DataType(DataType.Date)]
        public DateTime BirthDate { get; set; }

        public string Gender { get; set; } = string.Empty;

        public string NationalId { get; set; } = string.Empty;

        public string Nationality { get; set; } = string.Empty;

        // الحالة الدراسية

        public string Status { get; set; } = string.Empty;

        public decimal? GraduationAverage { get; set; }

        // صور ووثائق الطالب

        public string? PersonalImage { get; set; }

        public string? NationalIdImage { get; set; }

        public string? CertificateImage { get; set; }

        // بيانات حساب الطالب

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [DataType(DataType.Password)]
        [Compare("Password",
            ErrorMessage = "كلمتا المرور غير متطابقتين")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}