using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentAffairsManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmployeeFromCourseOfferings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_COURSE_OFFERINGS_EMPLOYEES_EmployeeId",
                table: "COURSE_OFFERINGS");

            migrationBuilder.DropIndex(
                name: "IX_COURSE_OFFERINGS_EmployeeId",
                table: "COURSE_OFFERINGS");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "COURSE_OFFERINGS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EmployeeId",
                table: "COURSE_OFFERINGS",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_COURSE_OFFERINGS_EmployeeId",
                table: "COURSE_OFFERINGS",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_COURSE_OFFERINGS_EMPLOYEES_EmployeeId",
                table: "COURSE_OFFERINGS",
                column: "EmployeeId",
                principalTable: "EMPLOYEES",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
