using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AbraqAccount.Migrations
{
    /// <inheritdoc />
    public partial class SwitchVendorToBankMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseItems_Vendors_VendorId",
                table: "PurchaseItems");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseItems_BankMasters_VendorId",
                table: "PurchaseItems",
                column: "VendorId",
                principalTable: "BankMasters",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseItems_BankMasters_VendorId",
                table: "PurchaseItems");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseItems_Vendors_VendorId",
                table: "PurchaseItems",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id");
        }
    }
}
