
namespace StudentAffairsManagementSystem.Models
{
    public class CourseOffering
    {
        public long Id { get; set; }

        public long CourseId { get; set; }

        public long SemesterId { get; set; }

        public int Capacity { get; set; }

        public Course Course { get; set; } = null!;

        public Semester Semester { get; set; } = null!;
    }
}
