using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enakliyat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrierBillingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "Carriers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceAddress",
                table: "Carriers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandlinePhone",
                table: "Carriers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxNumber",
                table: "Carriers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxOffice",
                table: "Carriers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Carriers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Carriers_DistrictId",
                table: "Carriers",
                column: "DistrictId");

            migrationBuilder.AddForeignKey(
                name: "FK_Carriers_Districts_DistrictId",
                table: "Carriers",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Carriers_Districts_DistrictId",
                table: "Carriers");

            migrationBuilder.DropIndex(
                name: "IX_Carriers_DistrictId",
                table: "Carriers");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "Carriers");

            migrationBuilder.DropColumn(
                name: "InvoiceAddress",
                table: "Carriers");

            migrationBuilder.DropColumn(
                name: "LandlinePhone",
                table: "Carriers");

            migrationBuilder.DropColumn(
                name: "TaxNumber",
                table: "Carriers");

            migrationBuilder.DropColumn(
                name: "TaxOffice",
                table: "Carriers");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Carriers");
        }
    }
}
