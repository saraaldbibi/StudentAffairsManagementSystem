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
                .GetRequiredService<
                    RoleManager<IdentityRole<long>>>();

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
                // التحقق من أدوار Identity الحقيقية
                var roles = await userManager.GetRolesAsync(admin);

                if (roles.Count != 1 ||
                    roles[0] != adminRole ||
                    admin.Role != adminRole)
                {
                    throw new InvalidOperationException(
                        "حساب admin موجود، لكن صلاحياته " +
                        "غير متطابقة مع دور Admin. " +
                        "يجب مراجعة الحساب يدوياً.");
                }

                // لا نعيد إنشاء الحساب ولا نغير كلمة مروره
                return;
            }

            // قراءة كلمة المرور من الإعدادات
            // يُفضّل تخزينها في User Secrets أثناء التطوير
            string? password =
                configuration["AdminSeed:Password"];

            // إذا لم توجد كلمة مرور، نتجاوز الإنشاء
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
                account, password);

            if (!createResult.Succeeded)
            {
                string errors = string.Join(
                    "; ",
                    createResult.Errors.Select(
                        e => e.Description));

                throw new InvalidOperationException(
                    "تعذر إنشاء حساب المدير: " + errors);
            }

            // ربط الحساب بدور Admin في Identity
            var roleResult = await userManager.AddToRoleAsync(
                account, adminRole);

            if (!roleResult.Succeeded)
            {
                // محاولة تنظيف الحساب ناقص الإعداد
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
                        ". وتعذر حذف الحساب ناقص الإعداد: " +
                        deleteErrors);
                }

                throw new InvalidOperationException(
                    "تعذر ربط المدير بدور Admin: " +
                    errors);
            }
        }
    }
}