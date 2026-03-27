using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enakliyat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMoveRequestTrackingToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingToken",
                table: "MoveRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoveRequests_TrackingToken",
                table: "MoveRequests",
                column: "TrackingToken",
                unique: true,
                filter: "[TrackingToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MoveRequests_TrackingToken",
                table: "MoveRequests");

            migrationBuilder.DropColumn(
                name: "TrackingToken",
                table: "MoveRequests");
        }
    }
}
