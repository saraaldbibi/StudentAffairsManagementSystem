
SELECT
    a.Id AS AccountId,
    a.UserName,
    a.Role AS AccountRole,
    r.Name AS IdentityRole,
    e.EmployeeNumber
FROM dbo.ACCOUNTS a
LEFT JOIN dbo.AspNetUserRoles ur
    ON a.Id = ur.UserId
LEFT JOIN dbo.AspNetRoles r
    ON ur.RoleId = r.Id
LEFT JOIN dbo.EMPLOYEES e
    ON e.AccountId = a.Id
WHERE a.Role = 'Employee';
