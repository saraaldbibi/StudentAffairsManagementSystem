using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentAffairsManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionsAndDelegation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PERMISSIONS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PERMISSIONS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACCOUNT_PERMISSIONS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false),
                    GrantedByAccountId = table.Column<long>(type: "bigint", nullable: false),
                    CanDelegate = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParentGrantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCOUNT_PERMISSIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACCOUNT_PERMISSIONS_ACCOUNTS_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACCOUNT_PERMISSIONS_ACCOUNTS_GrantedByAccountId",
                        column: x => x.GrantedByAccountId,
                        principalTable: "ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACCOUNT_PERMISSIONS_ACCOUNT_PERMISSIONS_ParentGrantId",
                        column: x => x.ParentGrantId,
                        principalTable: "ACCOUNT_PERMISSIONS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACCOUNT_PERMISSIONS_PERMISSIONS_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "PERMISSIONS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACCOUNT_PERMISSIONS_AccountId_PermissionId_IsActive",
                table: "ACCOUNT_PERMISSIONS",
                columns: new[] { "AccountId", "PermissionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ACCOUNT_PERMISSIONS_GrantedByAccountId",
                table: "ACCOUNT_PERMISSIONS",
                column: "GrantedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACCOUNT_PERMISSIONS_ParentGrantId",
                table: "ACCOUNT_PERMISSIONS",
                column: "ParentGrantId");

            migrationBuilder.CreateIndex(
                name: "IX_ACCOUNT_PERMISSIONS_PermissionId",
                table: "ACCOUNT_PERMISSIONS",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PERMISSIONS_Code",
                table: "PERMISSIONS",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACCOUNT_PERMISSIONS");

            migrationBuilder.DropTable(
                name: "PERMISSIONS");
        }
    }
}
