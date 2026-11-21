using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AdminCatalogConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ResourceKey",
                table: "AdminCatalogEntries",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "AdminCatalogEntries",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "AdminCatalogEntries",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StationPartBalances_ValidQuantities",
                table: "StationPartBalances",
                sql: "[AllocatedQuantity] >= 0 AND [UsedQuantity] >= 0 AND [UsedQuantity] <= [AllocatedQuantity]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryBalances_NonNegative",
                table: "InventoryBalances",
                sql: "[Quantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryBalances_OneItem",
                table: "InventoryBalances",
                sql: "([ProductId] IS NOT NULL AND [ComponentId] IS NULL) OR ([ProductId] IS NULL AND [ComponentId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StationPartBalances_ValidQuantities",
                table: "StationPartBalances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryBalances_NonNegative",
                table: "InventoryBalances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryBalances_OneItem",
                table: "InventoryBalances");

            migrationBuilder.AlterColumn<string>(
                name: "ResourceKey",
                table: "AdminCatalogEntries",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "AdminCatalogEntries",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "AdminCatalogEntries",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60);
        }
    }
}
