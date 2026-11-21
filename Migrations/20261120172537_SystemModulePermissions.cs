using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class SystemModulePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermissionModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    GroupKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionModules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissionModuleId = table.Column<int>(type: "int", nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionDetails_PermissionModules_PermissionModuleId",
                        column: x => x.PermissionModuleId,
                        principalTable: "PermissionModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleModulePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PermissionModuleId = table.Column<int>(type: "int", nullable: false),
                    MenuVisible = table.Column<bool>(type: "bit", nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    CanCreate = table.Column<bool>(type: "bit", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleModulePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleModulePermissions_PermissionModules_PermissionModuleId",
                        column: x => x.PermissionModuleId,
                        principalTable: "PermissionModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleModulePermissions_User_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissionDetails",
                columns: table => new
                {
                    RoleModulePermissionId = table.Column<int>(type: "int", nullable: false),
                    PermissionDetailId = table.Column<int>(type: "int", nullable: false),
                    IsGranted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissionDetails", x => new { x.RoleModulePermissionId, x.PermissionDetailId });
                    table.ForeignKey(
                        name: "FK_RolePermissionDetails_PermissionDetails_PermissionDetailId",
                        column: x => x.PermissionDetailId,
                        principalTable: "PermissionDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissionDetails_RoleModulePermissions_RoleModulePermissionId",
                        column: x => x.RoleModulePermissionId,
                        principalTable: "RoleModulePermissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionDetails_PermissionModuleId_PermissionKey",
                table: "PermissionDetails",
                columns: new[] { "PermissionModuleId", "PermissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionModules_ModuleKey",
                table: "PermissionModules",
                column: "ModuleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleModulePermissions_PermissionModuleId",
                table: "RoleModulePermissions",
                column: "PermissionModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleModulePermissions_RoleName_PermissionModuleId",
                table: "RoleModulePermissions",
                columns: new[] { "RoleName", "PermissionModuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleModulePermissions_UpdatedByUserId",
                table: "RoleModulePermissions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissionDetails_PermissionDetailId",
                table: "RolePermissionDetails",
                column: "PermissionDetailId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissionDetails");

            migrationBuilder.DropTable(
                name: "PermissionDetails");

            migrationBuilder.DropTable(
                name: "RoleModulePermissions");

            migrationBuilder.DropTable(
                name: "PermissionModules");
        }
    }
}
