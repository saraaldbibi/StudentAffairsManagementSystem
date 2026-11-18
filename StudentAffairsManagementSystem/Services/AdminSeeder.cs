
using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Services
{
    public static class AdminSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services,
            IConfiguration configuration)
        {
            var userManager = services
                .GetRequiredService<UserManager<Account>>();

            var roleManager = services
                .GetRequiredService<RoleManager<IdentityRole<long>>>();

            const string adminUserName = "admin";
            const string adminRole = "Admin";

            // التأكد من وجود دور المدير
            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                throw new InvalidOperationException(
                    "دور Admin غير موجود في قاعدة البيانات.");
            }

            // البحث عن حساب المدير
            var admin = await userManager.FindByNameAsync(
                adminUserName);

            if (admin != null)
            {
                // التحقق من تطابق أدوار المدير
                var roles = await userManager.GetRolesAsync(admin);

                if (roles.Count != 1 ||
                    roles[0] != adminRole ||
                    admin.Role != adminRole)
                {
                    throw new InvalidOperationException(
                        "حساب admin موجود لكن صلاحياته غير متطابقة.");
                }

                // إعادة تعيين كلمة المرور فقط عند طلب ذلك
                bool resetRequested =
                    configuration.GetValue<bool>(
                        "AdminSeed:ResetPasswordOnce");

                if (resetRequested)
                {
                    string? newPassword =
                        configuration["AdminSeed:Password"];

                    if (string.IsNullOrWhiteSpace(newPassword))
                    {
                        throw new InvalidOperationException(
                            "كلمة المرور الجديدة غير موجودة.");
                    }

                    // إنشاء رمز إعادة تعيين عبر Identity
                    string resetToken =
                        await userManager
                            .GeneratePasswordResetTokenAsync(admin);

                    // إعادة تعيين كلمة المرور بأمان
                    var resetResult =
                        await userManager.ResetPasswordAsync(
                            admin,
                            resetToken,
                            newPassword);

                    if (!resetResult.Succeeded)
                    {
                        string errors = string.Join(
                            "; ",
                            resetResult.Errors.Select(
                                e => e.Description));

                        throw new InvalidOperationException(
                            "تعذر إعادة تعيين كلمة مرور المدير: " +
                            errors);
                    }

                    Console.WriteLine(
                        "تمت إعادة تعيين كلمة مرور المدير بنجاح. " +
                        "يجب تعطيل ResetPasswordOnce الآن.");
                }

                return;
            }

            // قراءة كلمة المرور لإنشاء المدير لأول مرة
            string? password =
                configuration["AdminSeed:Password"];

            if (string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            // إنشاء حساب المدير
            var account = new Account
            {
                UserName = adminUserName,
                Role = adminRole
            };

            var createResult = await userManager.CreateAsync(
                account,
                password);

            if (!createResult.Succeeded)
            {
                string errors = string.Join(
                    "; ",
                    createResult.Errors.Select(
                        e => e.Description));

                throw new InvalidOperationException(
                    "تعذر إنشاء حساب المدير: " + errors);
            }

            // ربط المدير بدور Admin
            var roleResult = await userManager.AddToRoleAsync(
                account,
                adminRole);

            if (!roleResult.Succeeded)
            {
                var deleteResult =
                    await userManager.DeleteAsync(account);

                string errors = string.Join(
                    "; ",
                    roleResult.Errors.Select(
                        e => e.Description));

                if (!deleteResult.Succeeded)
                {
                    string deleteErrors = string.Join(
                        "; ",
                        deleteResult.Errors.Select(
                            e => e.Description));

                    throw new InvalidOperationException(
                        "تعذر ربط المدير بدور Admin: " +
                        errors +
                        ". وتعذر حذف الحساب: " +
                        deleteErrors);
                }

                throw new InvalidOperationException(
                    "تعذر ربط المدير بدور Admin: " +
                    errors);
            }
        }
    }
}
