using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enakliyat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdjustUserMoveRequestDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MoveRequests_Users_UserId",
                table: "MoveRequests");

            migrationBuilder.AddForeignKey(
                name: "FK_MoveRequests_Users_UserId",
                table: "MoveRequests",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MoveRequests_Users_UserId",
                table: "MoveRequests");

            migrationBuilder.AddForeignKey(
                name: "FK_MoveRequests_Users_UserId",
                table: "MoveRequests",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
