SELECT
    AccountId,
    PermissionId,
    GrantedByAccountId,
    COUNT(*) AS DuplicateCount
FROM dbo.ACCOUNT_PERMISSIONS
WHERE IsActive = 1
  AND RevokedAt IS NULL
GROUP BY
    AccountId,
    PermissionId,
    GrantedByAccountId
HAVING COUNT(*) > 1;