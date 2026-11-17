using System.ComponentModel.DataAnnotations;

namespace StudentAffairsManagementSystem.ViewModels
{
    public class CreateEmployeeViewModel
    {
        // الرقم الوظيفي
        [Required(ErrorMessage = "الرقم الوظيفي مطلوب")]
        public string EmployeeNumber { get; set; } = string.Empty;

        // معلومات الموظف
        [Required(ErrorMessage = "اسم الموظف مطلوب")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم الأب مطلوب")]
        public string FatherName { get; set; } = string.Empty;

        [Required(ErrorMessage = "الكنية مطلوبة")]
        public string LastName { get; set; } = string.Empty;

        // معلومات التواصل
        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        // كلمة مرور حساب الموظف
        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        // تأكيد كلمة المرور
        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [DataType(DataType.Password)]
        [Compare("Password",
            ErrorMessage = "كلمتا المرور غير متطابقتين")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}