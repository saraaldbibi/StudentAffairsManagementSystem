
namespace StudentAffairsManagementSystem.Models
{
    public class Department
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long FacultyId { get; set; }
        public bool IsActive { get; set; }

        public Faculty Faculty { get; set; } = null!;
    }
}
