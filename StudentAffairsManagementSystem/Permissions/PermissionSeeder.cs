using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;
using StudentAffairsManagementSystem.Permissions;

namespace StudentAffairsManagementSystem.Services
{
    public static class PermissionSeeder
    {
        public static async Task SeedAsync(
            ApplicationDbContext context)
        {
            var codes = typeof(PermissionCodes)
                .GetFields(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static)
                .Where(field => field.IsLiteral &&
                                !field.IsInitOnly &&
                                field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!)
                .Distinct()
                .ToList();

            var existingCodes = await context.Permissions
                .Select(p => p.Code)
                .ToListAsync();

            var existingSet = existingCodes
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newPermissions = codes
                .Where(code => !existingSet.Contains(code))
                .Select(code => new Permission
                {
                    Code = code,
                    Name = code.Replace(".", " - ")
                })
                .ToList();

            if (newPermissions.Count == 0)
            {
                return;
            }

            await context.Permissions
                .AddRangeAsync(newPermissions);

            await context.SaveChangesAsync();
        }
    }
}