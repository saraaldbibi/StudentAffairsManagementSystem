
namespace StudentAffairsManagementSystem.Models
{
    public class Course
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int CreditHours { get; set; }

        public long DepartmentId { get; set; }

        public Department Department { get; set; } = null!;
    }
}
