using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;

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

        // Check whether a user has a permission.
        public async Task<bool> HasPermissionAsync(
            long accountId,
            string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(permissionCode))
                return false;

            var account = await _userManager
                .FindByIdAsync(accountId.ToString());

            if (account == null)
                return false;

            if (await _userManager.IsInRoleAsync(account, "Admin"))
                return true;

            bool isEmployee = await _userManager
                .IsInRoleAsync(account, "Employee");

            bool isStudent = await _userManager
                .IsInRoleAsync(account, "Student");

            if (!isEmployee && !isStudent)
                return false;

            var grants = await _context.AccountPermissions
                .AsNoTracking()
                .Where(ap =>
                    ap.AccountId == accountId &&
                    ap.Permission.Code == permissionCode &&
                    ap.IsActive &&
                    ap.RevokedAt == null)
                .Select(ap => ap.Id)
                .ToListAsync();

            foreach (var grantId in grants)
            {
                if (await IsGrantValidAsync(
                    grantId, permissionCode))
                {
                    return true;
                }
            }

            return false;
        }

        // Check whether a user may delegate a permission.
        public async Task<bool> CanDelegateAsync(
            long accountId,
            string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(permissionCode))
                return false;

            var account = await _userManager
                .FindByIdAsync(accountId.ToString());

            if (account == null)
                return false;

            if (await _userManager.IsInRoleAsync(account, "Admin"))
                return true;

            if (!await _userManager.IsInRoleAsync(
                account, "Employee"))
            {
                return false;
            }

            var grants = await _context.AccountPermissions
                .AsNoTracking()
                .Where(ap =>
                    ap.AccountId == accountId &&
                    ap.Permission.Code == permissionCode &&
                    ap.CanDelegate &&
                    ap.IsActive &&
                    ap.RevokedAt == null)
                .Select(ap => ap.Id)
                .ToListAsync();

            foreach (var grantId in grants)
            {
                if (await IsGrantValidAsync(
                    grantId, permissionCode))
                {
                    return true;
                }
            }

            return false;
        }

        // Grant a permission.
        // The callerId MUST come from the authenticated
        // user on the server, never from request input.
        public async Task<bool> GrantPermissionAsync(
            long callerId,
            long targetAccountId,
            string permissionCode,
            bool canDelegate = false)
        {
            if (string.IsNullOrWhiteSpace(permissionCode))
                return false;

            if (callerId == targetAccountId)
                return false;

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var caller = await _userManager
                .FindByIdAsync(callerId.ToString());

            var target = await _userManager
                .FindByIdAsync(targetAccountId.ToString());

            if (caller == null || target == null)
                return false;

            bool callerIsAdmin = await _userManager
                .IsInRoleAsync(caller, "Admin");

            bool callerIsEmployee = await _userManager
                .IsInRoleAsync(caller, "Employee");

            bool targetIsEmployee = await _userManager
                .IsInRoleAsync(target, "Employee");

            bool targetIsStudent = await _userManager
                .IsInRoleAsync(target, "Student");

            // Reject ambiguous or unsupported target roles.
            if (targetIsEmployee == targetIsStudent ||
                await _userManager.IsInRoleAsync(target, "Admin"))
            {
                return false;
            }

            long? parentGrantId = null;

            if (callerIsAdmin)
            {
                // Admin may grant to employees or students.
                // Only employees may receive delegation rights.
                if (canDelegate && !targetIsEmployee)
                    return false;
            }
            else if (callerIsEmployee)
            {
                // Employees may grant only to students.
                if (!targetIsStudent || canDelegate)
                    return false;

                // Find a valid delegable parent grant.
                var possibleParents = await _context
                    .AccountPermissions
                    .AsNoTracking()
                    .Where(ap =>
                        ap.AccountId == callerId &&
                        ap.Permission.Code == permissionCode &&
                        ap.CanDelegate &&
                        ap.IsActive &&
                        ap.RevokedAt == null)
                    .Select(ap => ap.Id)
                    .ToListAsync();

                foreach (var candidateId in possibleParents)
                {
                    if (await IsGrantValidAsync(
                        candidateId, permissionCode))
                    {
                        parentGrantId = candidateId;
                        break;
                    }
                }

                if (!parentGrantId.HasValue)
                    return false;
            }
            else
            {
                // Students and other users cannot grant.
                return false;
            }

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p =>
                    p.Code == permissionCode);

            if (permission == null)
                return false;

            // Prevent an active duplicate from this grantor.
            bool alreadyGranted = await _context
                .AccountPermissions
                .AnyAsync(ap =>
                    ap.AccountId == targetAccountId &&
                    ap.PermissionId == permission.Id &&
                    ap.GrantedByAccountId == callerId &&
                    ap.IsActive &&
                    ap.RevokedAt == null);

            if (alreadyGranted)
                return false;

            var grant = new AccountPermission
            {
                AccountId = targetAccountId,
                PermissionId = permission.Id,
                GrantedByAccountId = callerId,
                CanDelegate = callerIsAdmin &&
                              targetIsEmployee &&
                              canDelegate,
                ParentGrantId = parentGrantId,
                IsActive = true,
                GrantedAt = DateTime.UtcNow,
                RevokedAt = null
            };

            _context.AccountPermissions.Add(grant);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return true;
        }

        // Revoke a permission grant by its record ID.
        public async Task<bool> RevokePermissionAsync(
            long callerId,
            long grantId)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var caller = await _userManager
                .FindByIdAsync(callerId.ToString());

            if (caller == null)
                return false;

            bool isAdmin = await _userManager
                .IsInRoleAsync(caller, "Admin");

            bool isEmployee = await _userManager
                .IsInRoleAsync(caller, "Employee");

            if (!isAdmin && !isEmployee)
                return false;

            var grant = await _context.AccountPermissions
                .FirstOrDefaultAsync(ap => ap.Id == grantId);

            if (grant == null ||
                !grant.IsActive ||
                grant.RevokedAt != null)
            {
                return false;
            }

            if (!isAdmin)
            {
                // Employee can revoke only their own
                // delegated grants to students.
                if (grant.GrantedByAccountId != callerId ||
                    !grant.ParentGrantId.HasValue)
                {
                    return false;
                }

                var target = await _userManager
                    .FindByIdAsync(grant.AccountId.ToString());

                if (target == null ||
                    !await _userManager.IsInRoleAsync(
                        target, "Student"))
                {
                    return false;
                }
            }

            grant.IsActive = false;
            grant.RevokedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Descendant grants automatically become invalid
            // when their parent is revoked, because
            // IsGrantValidAsync checks the entire chain.
            return true;
        }

        // Get all grants for a target account.
        // The caller must be Admin, or an Employee
        // viewing grants they personally issued.
        public async Task<List<AccountPermission>>
            GetPermissionsAsync(
                long callerId,
                long targetAccountId)
        {
            var caller = await _userManager
                .FindByIdAsync(callerId.ToString());

            if (caller == null)
                return new List<AccountPermission>();

            bool isAdmin = await _userManager
                .IsInRoleAsync(caller, "Admin");

            bool isEmployee = await _userManager
                .IsInRoleAsync(caller, "Employee");

            if (!isAdmin && !isEmployee)
                return new List<AccountPermission>();

            var query = _context.AccountPermissions
                .AsNoTracking()
                .Include(ap => ap.Permission)
                .Where(ap => ap.AccountId == targetAccountId);

            if (!isAdmin)
            {
                query = query.Where(ap =>
                    ap.GrantedByAccountId == callerId &&
                    ap.ParentGrantId != null);
            }

            return await query
                .OrderByDescending(ap => ap.GrantedAt)
                .ToListAsync();
        }

        // Validate a grant and its complete parent chain.
        private async Task<bool> IsGrantValidAsync(
            long grantId,
            string permissionCode)
        {
            var visitedIds = new HashSet<long>();
            long? currentGrantId = grantId;

            const int maxDepth = 100;
            int depth = 0;

            while (currentGrantId.HasValue)
            {
                if (depth++ >= maxDepth)
                    return false;

                if (!visitedIds.Add(currentGrantId.Value))
                    return false;

                var grant = await _context.AccountPermissions
                    .AsNoTracking()
                    .Include(ap => ap.Permission)
                    .FirstOrDefaultAsync(ap =>
                        ap.Id == currentGrantId.Value);

                if (grant == null ||
                    !grant.IsActive ||
                    grant.RevokedAt != null ||
                    grant.Permission.Code != permissionCode)
                {
                    return false;
                }

                if (!grant.ParentGrantId.HasValue)
                {
                    // A root grant must come from an Admin.
                    var grantor = await _userManager
                        .FindByIdAsync(
                            grant.GrantedByAccountId.ToString());

                    if (grantor == null)
                        return false;

                    return await _userManager
                        .IsInRoleAsync(grantor, "Admin");
                }

                // A student grant cannot be delegated further.
                if (grant.CanDelegate)
                    return false;

                var parent = await _context.AccountPermissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ap =>
                        ap.Id == grant.ParentGrantId.Value);

                if (parent == null ||
                    parent.AccountId != grant.GrantedByAccountId ||
                    !parent.CanDelegate)
                {
                    return false;
                }

                var grantorAccount = await _userManager
                    .FindByIdAsync(
                        grant.GrantedByAccountId.ToString());

                if (grantorAccount == null ||
                    !await _userManager.IsInRoleAsync(
                        grantorAccount, "Employee"))
                {
                    return false;
                }

                var recipientAccount = await _userManager
                    .FindByIdAsync(
                        grant.AccountId.ToString());

                if (recipientAccount == null ||
                    !await _userManager.IsInRoleAsync(
                        recipientAccount, "Student"))
                {
                    return false;
                }

                currentGrantId = grant.ParentGrantId;
            }

            return false;
        }
    }
}