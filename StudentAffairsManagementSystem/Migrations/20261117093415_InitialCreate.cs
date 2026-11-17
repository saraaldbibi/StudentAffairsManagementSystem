using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentAffairsManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ACCOUNTS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCOUNTS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FACULTIES",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FACULTIES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SEMESTERS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SemesterNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AcademicYear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SEMESTERS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_ACCOUNTS_UserId",
                        column: x => x.UserId,
                        principalTable: "ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_ACCOUNTS_UserId",
                        column: x => x.UserId,
                        principalTable: "ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_ACCOUNTS_UserId",
                        column: x => x.UserId,
                        principalTable: "ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EMPLOYEES",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FatherName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EMPLOYEES", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EMPLOYEES_ACCOUNTS_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_ACCOUNTS_UserId",
                        column: x => x.UserId,
                        principalTable: "ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DEPARTMENTS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FacultyId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DEPARTMENTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DEPARTMENTS_FACULTIES_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "FACULTIES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COURSES",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreditHours = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COURSES", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COURSES_DEPARTMENTS_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "DEPARTMENTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HOUR_PRICES",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    SemesterId = table.Column<long>(type: "bigint", nullable: false),
                    PricePerHour = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HOUR_PRICES", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HOUR_PRICES_DEPARTMENTS_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "DEPARTMENTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HOUR_PRICES_SEMESTERS_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "SEMESTERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "STUDENTS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UniversityNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FatherName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MotherName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Governorate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BirthDate = table.Column<DateTime>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdmissionSemesterId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GraduationSemesterId = table.Column<long>(type: "bigint", nullable: true),
                    GraduationAverage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PersonalImage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NationalIdImage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertificateImage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STUDENTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_STUDENTS_ACCOUNTS_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_STUDENTS_DEPARTMENTS_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "DEPARTMENTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_STUDENTS_SEMESTERS_AdmissionSemesterId",
                        column: x => x.AdmissionSemesterId,
                        principalTable: "SEMESTERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_STUDENTS_SEMESTERS_GraduationSemesterId",
                        column: x => x.GraduationSemesterId,
                        principalTable: "SEMESTERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COURSE_OFFERINGS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    SemesterId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COURSE_OFFERINGS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COURSE_OFFERINGS_COURSES_CourseId",
                        column: x => x.CourseId,
                        principalTable: "COURSES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COURSE_OFFERINGS_EMPLOYEES_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "EMPLOYEES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COURSE_OFFERINGS_SEMESTERS_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "SEMESTERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REGISTRATIONS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    CourseOfferingId = table.Column<long>(type: "bigint", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REGISTRATIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REGISTRATIONS_COURSE_OFFERINGS_CourseOfferingId",
                        column: x => x.CourseOfferingId,
                        principalTable: "COURSE_OFFERINGS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REGISTRATIONS_STUDENTS_StudentId",
                        column: x => x.StudentId,
                        principalTable: "STUDENTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "ACCOUNTS",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "ACCOUNTS",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_COURSE_OFFERINGS_CourseId",
                table: "COURSE_OFFERINGS",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_COURSE_OFFERINGS_EmployeeId",
                table: "COURSE_OFFERINGS",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_COURSE_OFFERINGS_SemesterId",
                table: "COURSE_OFFERINGS",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_COURSES_DepartmentId",
                table: "COURSES",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DEPARTMENTS_FacultyId",
                table: "DEPARTMENTS",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_EMPLOYEES_AccountId",
                table: "EMPLOYEES",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EMPLOYEES_EmployeeNumber",
                table: "EMPLOYEES",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HOUR_PRICES_DepartmentId",
                table: "HOUR_PRICES",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HOUR_PRICES_SemesterId",
                table: "HOUR_PRICES",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_REGISTRATIONS_CourseOfferingId",
                table: "REGISTRATIONS",
                column: "CourseOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_REGISTRATIONS_StudentId",
                table: "REGISTRATIONS",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_STUDENTS_AccountId",
                table: "STUDENTS",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_STUDENTS_AdmissionSemesterId",
                table: "STUDENTS",
                column: "AdmissionSemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_STUDENTS_DepartmentId",
                table: "STUDENTS",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_STUDENTS_GraduationSemesterId",
                table: "STUDENTS",
                column: "GraduationSemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_STUDENTS_UniversityNumber",
                table: "STUDENTS",
                column: "UniversityNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "HOUR_PRICES");

            migrationBuilder.DropTable(
                name: "REGISTRATIONS");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "COURSE_OFFERINGS");

            migrationBuilder.DropTable(
                name: "STUDENTS");

            migrationBuilder.DropTable(
                name: "COURSES");

            migrationBuilder.DropTable(
                name: "EMPLOYEES");

            migrationBuilder.DropTable(
                name: "SEMESTERS");

            migrationBuilder.DropTable(
                name: "DEPARTMENTS");

            migrationBuilder.DropTable(
                name: "ACCOUNTS");

            migrationBuilder.DropTable(
                name: "FACULTIES");
        }
    }
}
