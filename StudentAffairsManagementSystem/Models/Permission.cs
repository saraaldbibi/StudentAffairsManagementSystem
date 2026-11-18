using System.ComponentModel.DataAnnotations;

namespace StudentAffairsManagementSystem.Models
{
    public class Permission
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
    }
}