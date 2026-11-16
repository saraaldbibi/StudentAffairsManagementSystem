
namespace StudentAffairsManagementSystem.Models
{
    public class HourPrice
    {
        public long Id { get; set; }

        public long DepartmentId { get; set; }
        public long SemesterId { get; set; }

        public decimal PricePerHour { get; set; }

        public Department Department { get; set; } = null!;
        public Semester Semester { get; set; } = null!;
    }
}
