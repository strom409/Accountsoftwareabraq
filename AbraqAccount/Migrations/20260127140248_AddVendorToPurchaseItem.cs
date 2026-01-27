using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AbraqAccount.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorToPurchaseItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VendorId",
                table: "PurchaseItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_VendorId",
                table: "PurchaseItems",
                column: "VendorId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseItems_Vendors_VendorId",
                table: "PurchaseItems",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseItems_Vendors_VendorId",
                table: "PurchaseItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseItems_VendorId",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "PurchaseItems");
        }
    }
}
