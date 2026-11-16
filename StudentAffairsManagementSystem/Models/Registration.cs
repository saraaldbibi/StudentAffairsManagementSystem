
namespace StudentAffairsManagementSystem.Models
{
    public class Registration
    {
        public long Id { get; set; }

        public long StudentId { get; set; }
        public long CourseOfferingId { get; set; }

        public DateTime RegisteredAt { get; set; }
        public string Status { get; set; } = string.Empty;

        public Student Student { get; set; } = null!;
        public CourseOffering CourseOffering { get; set; } = null!;
    }
}
