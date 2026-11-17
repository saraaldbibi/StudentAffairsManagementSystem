using Microsoft.AspNetCore.Identity;

namespace StudentAffairsManagementSystem.Models
{
    public class Account : IdentityUser<long>
    {
        public string Role { get; set; } = string.Empty;
    }
}