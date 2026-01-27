using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AbraqAccount.Migrations
{
    /// <inheritdoc />
    public partial class UnifyVendorSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Vendors_VendorId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReceives_Vendors_VendorId",
                table: "PurchaseReceives");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_BankMasters_VendorId",
                table: "PurchaseOrders",
                column: "VendorId",
                principalTable: "BankMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReceives_BankMasters_VendorId",
                table: "PurchaseReceives",
                column: "VendorId",
                principalTable: "BankMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_BankMasters_VendorId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReceives_BankMasters_VendorId",
                table: "PurchaseReceives");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Vendors_VendorId",
                table: "PurchaseOrders",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReceives_Vendors_VendorId",
                table: "PurchaseReceives",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
