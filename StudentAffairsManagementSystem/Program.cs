using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Controllers;
using StudentAffairsManagementSystem.Data;
using StudentAffairsManagementSystem.Models;
using StudentAffairsManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// =====================================
// MVC
// =====================================
builder.Services.AddControllersWithViews();

// =====================================
// Database
// =====================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")
    )
);

// =====================================
// Identity Configuration
// =====================================
builder.Services
    .AddIdentity<Account, IdentityRole<long>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// =====================================
// Authentication Cookie
// =====================================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;

    // Return 401 for unauthenticated backend requests
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/auth-test") ||
            context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    // Return 403 for unauthorized backend requests
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/auth-test") ||
            context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// =====================================
// Permission Service
// =====================================
builder.Services.AddScoped<PermissionService>();

// =====================================
// Build Application
// =====================================
var app = builder.Build();

// =====================================
// Initialize Roles, Admin and Permissions
// =====================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var roleManager = services.GetRequiredService<
        RoleManager<IdentityRole<long>>>();

    // Create basic Identity roles
    await AccountController.EnsureRolesCreatedAsync(
        roleManager);

    // Create initial Admin
    await AdminSeeder.SeedAsync(
        services,
        app.Configuration);

    // Seed permission definitions
    var dbContext = services.GetRequiredService<
        ApplicationDbContext>();

    await PermissionSeeder.SeedAsync(dbContext);

    // Create test employee only in Development
    if (app.Environment.IsDevelopment())
    {
        await EmployeeTestSeeder.SeedAsync(
            services,
            app.Configuration);
    }
}

// =====================================
// HTTP Pipeline
// =====================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();