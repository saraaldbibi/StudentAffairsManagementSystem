
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
    }
}
