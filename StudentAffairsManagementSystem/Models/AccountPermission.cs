using System;
using System.ComponentModel.DataAnnotations;

namespace StudentAffairsManagementSystem.Models
{
    public class AccountPermission
    {
        public long Id { get; set; }

        // الحساب الذي حصل على الصلاحية
        [Required]
        public long AccountId { get; set; }

        public Account Account { get; set; } = null!;

        // الصلاحية الممنوحة
        [Required]
        public long PermissionId { get; set; }

        public Permission Permission { get; set; } = null!;

        // الحساب الذي منح الصلاحية
        [Required]
        public long GrantedByAccountId { get; set; }

        public Account GrantedByAccount { get; set; } = null!;

        // هل يمكن للمستخدم تفويض هذه الصلاحية؟
        public bool CanDelegate { get; set; } = false;

        // هل الصلاحية فعالة؟
        public bool IsActive { get; set; } = true;

        // تاريخ منح الصلاحية
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        // تاريخ إلغاء الصلاحية إن وُجد
        public DateTime? RevokedAt { get; set; }

        // سجل الصلاحية الأصلية التي سمحت بالتفويض
        // يكون فارغاً إذا منحها الأدمن مباشرة
        public long? ParentGrantId { get; set; }

        public AccountPermission? ParentGrant { get; set; }
    }
}