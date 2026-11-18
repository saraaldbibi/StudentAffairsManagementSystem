using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentAffairsManagementSystem.Services;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    [Route("api/permissions")]
    public class PermissionManagementController : Controller
    {
        private readonly PermissionService _permissionService;

        public PermissionManagementController(
            PermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        // Get the authenticated account ID.
        // Never accept the caller ID from request input.
        private long? GetCurrentAccountId()
        {
            var value = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (long.TryParse(value, out long accountId))
                return accountId;

            return null;
        }

        // Request model for granting a permission.
        public class GrantPermissionRequest
        {
            public long TargetAccountId { get; set; }

            public string PermissionCode { get; set; }
                = string.Empty;

            public bool CanDelegate { get; set; }
        }

        // Request model for revoking a permission.
        public class RevokePermissionRequest
        {
            public long GrantId { get; set; }
        }

        // =====================================
        // Grant permission
        // POST /api/permissions/grant
        // =====================================
        [HttpPost("grant")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grant(
            [FromForm] GrantPermissionRequest request)
        {
            var callerId = GetCurrentAccountId();

            if (!callerId.HasValue)
                return Unauthorized();

            if (request.TargetAccountId <= 0 ||
                string.IsNullOrWhiteSpace(
                    request.PermissionCode))
            {
                return BadRequest(
                    "Invalid permission request.");
            }

            bool success = await _permissionService
                .GrantPermissionAsync(
                    callerId.Value,
                    request.TargetAccountId,
                    request.PermissionCode,
                    request.CanDelegate);

            if (!success)
            {
                return BadRequest(
                    "Permission grant rejected. " +
                    "Check roles, delegation rights, " +
                    "or duplicate grants.");
            }

            return Ok(
                "Permission granted successfully.");
        }

        // =====================================
        // Revoke permission
        // POST /api/permissions/revoke
        // =====================================
        [HttpPost("revoke")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(
            [FromForm] RevokePermissionRequest request)
        {
            var callerId = GetCurrentAccountId();

            if (!callerId.HasValue)
                return Unauthorized();

            if (request.GrantId <= 0)
                return BadRequest("Invalid grant ID.");

            bool success = await _permissionService
                .RevokePermissionAsync(
                    callerId.Value,
                    request.GrantId);

            if (!success)
            {
                return BadRequest(
                    "Permission revocation rejected.");
            }

            return Ok(
                "Permission revoked successfully.");
        }

        // =====================================
        // View permission grants for an account
        // GET /api/permissions/account/5
        // =====================================
        [HttpGet("account/{accountId:long}")]
        public async Task<IActionResult> GetAccountPermissions(
            long accountId)
        {
            var callerId = GetCurrentAccountId();

            if (!callerId.HasValue)
                return Unauthorized();

            if (accountId <= 0)
                return BadRequest("Invalid account ID.");

            var grants = await _permissionService
                .GetPermissionsAsync(
                    callerId.Value,
                    accountId);

            var result = grants.Select(grant => new
            {
                grant.Id,
                grant.AccountId,
                PermissionCode = grant.Permission.Code,
                grant.GrantedByAccountId,
                grant.CanDelegate,
                grant.IsActive,
                grant.GrantedAt,
                grant.RevokedAt,
                grant.ParentGrantId
            });

            return Ok(result);
        }
    }
}