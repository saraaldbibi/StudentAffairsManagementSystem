
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;
using StudentAffairsManagementSystem.Permissions;

namespace StudentAffairsManagementSystem.Services
{
    public class PermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Account> _userManager;

        public PermissionService(
            ApplicationDbContext context,
            UserManager<Account> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // التحقق من صلاحية المستخدم الحالي
        // ==========================================
        public async Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionCode)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return false;

            if (string.IsNullOrWhiteSpace(permissionCode))
                return false;

            if (user.IsInRole("Admin"))
                return true;

            if (!user.IsInRole("Employee"))
                return false;

            string? accountIdValue =
                _userManager.GetUserId(user);

            if (!long.TryParse(
                accountIdValue,
                out long accountId))
            {
                return false;
            }

            return await _context.AccountPermissions
                .AsNoTracking()
                .AnyAsync(ap =>
                    ap.AccountId == accountId &&
                    ap.IsActive &&
                    ap.RevokedAt == null &&
                    ap.Permission.Code == permissionCode);
        }

        // ==========================================
        // صلاحية حذف الطلاب
        // ==========================================
        public async Task<bool> CanDeleteStudentsAsync(
            ClaimsPrincipal user)
        {
            return await HasPermissionAsync(
                user,
                PermissionCodes.StudentsDelete);
        }

        // ==========================================
        // منح صلاحية لموظف
        // ==========================================
        public async Task<bool> GrantPermissionAsync(
            long callerAccountId,
            long targetAccountId,
            string permissionCode,
            bool canDelegate)
        {
            if (callerAccountId <= 0 ||
                targetAccountId <= 0 ||
                string.IsNullOrWhiteSpace(permissionCode))
            {
                return false;
            }

            if (callerAccountId == targetAccountId)
                return false;

            var caller = await _userManager.FindByIdAsync(
                callerAccountId.ToString());

            var target = await _userManager.FindByIdAsync(
                targetAccountId.ToString());

            if (caller == null || target == null)
                return false;

            bool callerIsAdmin =
                await _userManager.IsInRoleAsync(
                    caller, "Admin");

            bool callerIsEmployee =
                await _userManager.IsInRoleAsync(
                    caller, "Employee");

            bool targetIsEmployee =
                await _userManager.IsInRoleAsync(
                    target, "Employee");

            // منح الصلاحيات للموظفين فقط
            if (!targetIsEmployee)
                return false;

            if (!callerIsAdmin && !callerIsEmployee)
                return false;

            var permission = await _context.Permissions
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Code == permissionCode);

            if (permission == null)
                return false;

            long? parentGrantId = null;

            // الموظف يحتاج صلاحية تفويض فعالة
            if (!callerIsAdmin)
            {
                var parentGrant =
                    await _context.AccountPermissions
                        .AsNoTracking()
                        .Where(ap =>
                            ap.AccountId == callerAccountId &&
                            ap.PermissionId == permission.Id &&
                            ap.IsActive &&
                            ap.RevokedAt == null &&
                            ap.CanDelegate)
                        .OrderBy(ap => ap.Id)
                        .FirstOrDefaultAsync();

                if (parentGrant == null)
                    return false;

                parentGrantId = parentGrant.Id;

                // الموظف لا يستطيع منح حق التفويض
                canDelegate = false;
            }

            bool alreadyGranted =
                await _context.AccountPermissions
                    .AnyAsync(ap =>
                        ap.AccountId == targetAccountId &&
                        ap.PermissionId == permission.Id &&
                        ap.IsActive &&
                        ap.RevokedAt == null);

            if (alreadyGranted)
                return false;

            var grant = new AccountPermission
            {
                AccountId = targetAccountId,
                PermissionId = permission.Id,
                GrantedByAccountId = callerAccountId,
                CanDelegate = canDelegate,
                IsActive = true,
                GrantedAt = DateTime.UtcNow,
                RevokedAt = null,
                ParentGrantId = parentGrantId
            };

            _context.AccountPermissions.Add(grant);

            await _context.SaveChangesAsync();

            return true;
        }

        // ==========================================
        // سحب صلاحية موجودة
        // ==========================================
        public async Task<bool> RevokePermissionAsync(
            long callerAccountId,
            long grantId)
        {
            if (callerAccountId <= 0 || grantId <= 0)
                return false;

            var caller = await _userManager.FindByIdAsync(
                callerAccountId.ToString());

            if (caller == null)
                return false;

            bool callerIsAdmin =
                await _userManager.IsInRoleAsync(
                    caller, "Admin");

            bool callerIsEmployee =
                await _userManager.IsInRoleAsync(
                    caller, "Employee");

            if (!callerIsAdmin && !callerIsEmployee)
                return false;

            var grant = await _context.AccountPermissions
                .FirstOrDefaultAsync(ap =>
                    ap.Id == grantId &&
                    ap.IsActive &&
                    ap.RevokedAt == null);

            if (grant == null)
                return false;

            if (!callerIsAdmin)
            {
                // الموظف يسحب فقط الصلاحيات التي منحها
                if (grant.GrantedByAccountId != callerAccountId)
                    return false;

                bool stillCanDelegate =
                    await _context.AccountPermissions
                        .AnyAsync(ap =>
                            ap.AccountId == callerAccountId &&
                            ap.PermissionId == grant.PermissionId &&
                            ap.IsActive &&
                            ap.RevokedAt == null &&
                            ap.CanDelegate);

                if (!stillCanDelegate)
                    return false;
            }

            // سحب المنحة وجميع المنح التابعة لها
            var allGrants = await _context.AccountPermissions
                .Where(ap =>
                    ap.PermissionId == grant.PermissionId)
                .ToListAsync();

            var idsToRevoke = new HashSet<long>
            {
                grant.Id
            };

            bool added;

            do
            {
                added = false;

                foreach (var item in allGrants)
                {
                    if (item.ParentGrantId.HasValue &&
                        idsToRevoke.Contains(
                            item.ParentGrantId.Value) &&
                        idsToRevoke.Add(item.Id))
                    {
                        added = true;
                    }
                }
            }
            while (added);

            DateTime revokedAt = DateTime.UtcNow;

            foreach (var item in allGrants)
            {
                if (idsToRevoke.Contains(item.Id) &&
                    item.IsActive &&
                    item.RevokedAt == null)
                {
                    item.IsActive = false;
                    item.CanDelegate = false;
                    item.RevokedAt = revokedAt;
                }
            }

            await _context.SaveChangesAsync();

            return true;
        }

        // ==========================================
        // عرض صلاحيات حساب معين
        // ==========================================
        public async Task<List<AccountPermission>>
            GetPermissionsAsync(
                long callerAccountId,
                long targetAccountId)
        {
            if (callerAccountId <= 0 ||
                targetAccountId <= 0)
            {
                return new List<AccountPermission>();
            }

            var caller = await _userManager.FindByIdAsync(
                callerAccountId.ToString());

            if (caller == null)
                return new List<AccountPermission>();

            bool callerIsAdmin =
                await _userManager.IsInRoleAsync(
                    caller, "Admin");

            bool callerIsEmployee =
                await _userManager.IsInRoleAsync(
                    caller, "Employee");

            if (!callerIsAdmin)
            {
                if (!callerIsEmployee ||
                    callerAccountId != targetAccountId)
                {
                    return new List<AccountPermission>();
                }
            }

            return await _context.AccountPermissions
                .AsNoTracking()
                .Include(ap => ap.Permission)
                .Where(ap =>
                    ap.AccountId == targetAccountId)
                .OrderByDescending(ap => ap.GrantedAt)
                .ToListAsync();
        }
    }
}
