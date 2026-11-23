using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddWarrantyRequestAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarrantyRequestAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarrantyRequestId = table.Column<int>(type: "int", nullable: false),
                    StoredFileId = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyRequestAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarrantyRequestAttachments_StoredFile_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarrantyRequestAttachments_WarrantyRequests_WarrantyRequestId",
                        column: x => x.WarrantyRequestId,
                        principalTable: "WarrantyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyRequestAttachments_StoredFileId",
                table: "WarrantyRequestAttachments",
                column: "StoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyRequestAttachments_WarrantyRequestId_StoredFileId",
                table: "WarrantyRequestAttachments",
                columns: new[] { "WarrantyRequestId", "StoredFileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarrantyRequestAttachments");
        }
    }
}
