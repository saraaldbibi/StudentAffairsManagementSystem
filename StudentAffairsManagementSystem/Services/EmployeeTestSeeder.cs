using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Services
{
    public static class EmployeeTestSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services,
            IConfiguration configuration)
        {
            var employeeNumber =
                configuration["EmployeeTest:EmployeeNumber"];

            var password =
                configuration["EmployeeTest:Password"];

            // Skip when test credentials are not configured
            if (string.IsNullOrWhiteSpace(employeeNumber) ||
                string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            employeeNumber = employeeNumber.Trim();

            var context = services.GetRequiredService<
                ApplicationDbContext>();

            var userManager = services.GetRequiredService<
                UserManager<Account>>();

            // Do not recreate an existing account
            var existingAccount =
                await userManager.FindByNameAsync(employeeNumber);

            if (existingAccount != null)
            {
                return;
            }

            // Do not duplicate an employee number
            bool employeeExists = await context.Employees
                .AnyAsync(e =>
                    e.EmployeeNumber == employeeNumber);

            if (employeeExists)
            {
                throw new InvalidOperationException(
                    "Test employee number already exists.");
            }

            await using var transaction =
                await context.Database.BeginTransactionAsync();

            var account = new Account
            {
                UserName = employeeNumber,
                Role = "Employee"
            };

            var createResult =
                await userManager.CreateAsync(account, password);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create test employee account: " +
                    string.Join("; ",
                        createResult.Errors.Select(e =>
                            e.Description)));
            }

            var roleResult =
                await userManager.AddToRoleAsync(
                    account, "Employee");

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not assign Employee role: " +
                    string.Join("; ",
                        roleResult.Errors.Select(e =>
                            e.Description)));
            }

            var employee = new Employee
            {
                EmployeeNumber = employeeNumber,
                FirstName = "Test",
                FatherName = "Employee",
                LastName = "Account",
                Phone = "",
                Address = "",
                AccountId = account.Id
            };

            context.Employees.Add(employee);

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
    }
}