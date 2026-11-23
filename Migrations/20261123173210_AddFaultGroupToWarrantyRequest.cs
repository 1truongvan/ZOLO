using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddFaultGroupToWarrantyRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FaultGroupId",
                table: "WarrantyRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyRequests_FaultGroupId",
                table: "WarrantyRequests",
                column: "FaultGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_WarrantyRequests_AdminCatalogEntries_FaultGroupId",
                table: "WarrantyRequests",
                column: "FaultGroupId",
                principalTable: "AdminCatalogEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WarrantyRequests_AdminCatalogEntries_FaultGroupId",
                table: "WarrantyRequests");

            migrationBuilder.DropIndex(
                name: "IX_WarrantyRequests_FaultGroupId",
                table: "WarrantyRequests");

            migrationBuilder.DropColumn(
                name: "FaultGroupId",
                table: "WarrantyRequests");
        }
    }
}
