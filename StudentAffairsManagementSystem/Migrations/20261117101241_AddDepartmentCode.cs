using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentAffairsManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "DEPARTMENTS",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DEPARTMENTS_Code",
                table: "DEPARTMENTS",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DEPARTMENTS_Code",
                table: "DEPARTMENTS");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "DEPARTMENTS");
        }
    }
}
