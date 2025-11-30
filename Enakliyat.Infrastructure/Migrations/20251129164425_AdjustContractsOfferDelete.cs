using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enakliyat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdjustContractsOfferDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Offers_OfferId",
                table: "Contracts");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Offers_OfferId",
                table: "Contracts",
                column: "OfferId",
                principalTable: "Offers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Offers_OfferId",
                table: "Contracts");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Offers_OfferId",
                table: "Contracts",
                column: "OfferId",
                principalTable: "Offers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
