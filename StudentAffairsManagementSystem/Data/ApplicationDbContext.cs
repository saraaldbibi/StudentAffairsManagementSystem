
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Models;

namespace StudentAffairsManagementSystem.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<Account, IdentityRole<long>, long>
    {
        // Constructor
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Existing tables
        public DbSet<Faculty> Faculties { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Semester> Semesters { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<HourPrice> HourPrices { get; set; }
        public DbSet<CourseOffering> CourseOfferings { get; set; }
        public DbSet<Registration> Registrations { get; set; }

        // Permission tables
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<AccountPermission> AccountPermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ====================================
            // Table names
            // ====================================

            builder.Entity<Account>().ToTable("ACCOUNTS");
            builder.Entity<Faculty>().ToTable("FACULTIES");
            builder.Entity<Department>().ToTable("DEPARTMENTS");
            builder.Entity<Employee>().ToTable("EMPLOYEES");
            builder.Entity<Student>().ToTable("STUDENTS");
            builder.Entity<Semester>().ToTable("SEMESTERS");
            builder.Entity<Course>().ToTable("COURSES");
            builder.Entity<HourPrice>().ToTable("HOUR_PRICES");
            builder.Entity<CourseOffering>().ToTable("COURSE_OFFERINGS");
            builder.Entity<Registration>().ToTable("REGISTRATIONS");

            builder.Entity<Permission>().ToTable("PERMISSIONS");
            builder.Entity<AccountPermission>()
                .ToTable("ACCOUNT_PERMISSIONS");

            // ====================================
            // Unique indexes
            // ====================================

            builder.Entity<Student>()
                .HasIndex(s => s.UniversityNumber)
                .IsUnique();

            builder.Entity<Employee>()
                .HasIndex(e => e.EmployeeNumber)
                .IsUnique();

            builder.Entity<Department>()
                .HasIndex(d => d.Code)
                .IsUnique();

            // ====================================
            // Student -> Department
            // ====================================

            builder.Entity<Student>()
                .HasOne(s => s.Department)
                .WithMany()
                .HasForeignKey(s => s.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // Department -> Faculty
            // ====================================

            builder.Entity<Department>()
                .HasOne(d => d.Faculty)
                .WithMany()
                .HasForeignKey(d => d.FacultyId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // Course -> Department
            // ====================================

            builder.Entity<Course>()
                .HasOne(c => c.Department)
                .WithMany()
                .HasForeignKey(c => c.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // Employee -> Account
            // ====================================

            builder.Entity<Employee>()
                .HasOne(e => e.Account)
                .WithOne()
                .HasForeignKey<Employee>(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // Student -> Account
            // ====================================

            builder.Entity<Student>()
                .HasOne(s => s.Account)
                .WithOne()
                .HasForeignKey<Student>(s => s.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // Student -> Admission Semester
            // ====================================

            builder.Entity<Student>()
                .HasOne(s => s.AdmissionSemester)
                .WithMany()
                .HasForeignKey(s => s.AdmissionSemesterId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // Student -> Graduation Semester
            // ====================================

            builder.Entity<Student>()
                .HasOne(s => s.GraduationSemester)
                .WithMany()
                .HasForeignKey(s => s.GraduationSemesterId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // ====================================
            // HourPrice -> Department
            // ====================================

            builder.Entity<HourPrice>()
                .HasOne(h => h.Department)
                .WithMany()
                .HasForeignKey(h => h.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // HourPrice -> Semester
            // ====================================

            builder.Entity<HourPrice>()
                .HasOne(h => h.Semester)
                .WithMany()
                .HasForeignKey(h => h.SemesterId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // CourseOffering -> Course
            // ====================================

            builder.Entity<CourseOffering>()
                .HasOne(co => co.Course)
                .WithMany()
                .HasForeignKey(co => co.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // CourseOffering -> Semester
            // ====================================

            builder.Entity<CourseOffering>()
                .HasOne(co => co.Semester)
                .WithMany()
                .HasForeignKey(co => co.SemesterId)
                .OnDelete(DeleteBehavior.Restrict);

            // لا يوجد ارتباط بين المقرر المطروح وموظف محدد.
            // يمكن للموظفين المخولين إدارة التسجيلات
            // وفق الصلاحيات المعتمدة في النظام.

            // ====================================
            // Registration -> Student
            // ====================================

            builder.Entity<Registration>()
                .HasOne(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // Registration -> CourseOffering
            // ====================================

            builder.Entity<Registration>()
                .HasOne(r => r.CourseOffering)
                .WithMany()
                .HasForeignKey(r => r.CourseOfferingId)
                .OnDelete(DeleteBehavior.Restrict);

            // ====================================
            // Property configurations
            // ====================================

            builder.Entity<HourPrice>()
                .Property(h => h.PricePerHour)
                .HasPrecision(18, 2);

            builder.Entity<Student>()
                .Property(s => s.GraduationAverage)
                .HasPrecision(5, 2);

            builder.Entity<Student>()
                .Property(s => s.BirthDate)
                .HasColumnType("date");

            builder.Entity<Semester>()
                .Property(s => s.StartDate)
                .HasColumnType("date");

            builder.Entity<Semester>()
                .Property(s => s.EndDate)
                .HasColumnType("date");

            // ====================================
            // Permission configurations
            // ====================================

            // Every permission must have a unique code
            builder.Entity<Permission>()
                .HasIndex(p => p.Code)
                .IsUnique();

            // AccountPermission -> Account receiving permission
            builder.Entity<AccountPermission>()
                .HasOne(ap => ap.Account)
                .WithMany()
                .HasForeignKey(ap => ap.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // AccountPermission -> Permission
            builder.Entity<AccountPermission>()
                .HasOne(ap => ap.Permission)
                .WithMany()
                .HasForeignKey(ap => ap.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);

            // AccountPermission -> Account granting permission
            builder.Entity<AccountPermission>()
                .HasOne(ap => ap.GrantedByAccount)
                .WithMany()
                .HasForeignKey(ap => ap.GrantedByAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Delegated permission -> Original permission grant
            builder.Entity<AccountPermission>()
                .HasOne(ap => ap.ParentGrant)
                .WithMany()
                .HasForeignKey(ap => ap.ParentGrantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Speed up permission lookups
            builder.Entity<AccountPermission>()
                .HasIndex(ap => new
                {
                    ap.AccountId,
                    ap.PermissionId,
                    ap.IsActive
                });

            // Speed up finding delegated permissions
            builder.Entity<AccountPermission>()
                .HasIndex(ap => ap.ParentGrantId);
        }
    }
}
