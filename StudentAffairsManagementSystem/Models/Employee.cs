namespace StudentAffairsManagementSystem.Models
{
    public class Employee
    {
        public long Id { get; set; }

        // الرقم الوظيفي
        public string EmployeeNumber { get; set; } = string.Empty;

        // معلومات الموظف
        public string FirstName { get; set; } = string.Empty;

        public string FatherName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        // معلومات التواصل
        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        // ربط الموظف بحسابه
        public long AccountId { get; set; }

        public Account Account { get; set; } = null!;
    }
}