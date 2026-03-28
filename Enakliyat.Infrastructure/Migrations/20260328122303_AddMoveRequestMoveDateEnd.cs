using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enakliyat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMoveRequestMoveDateEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MoveDateEnd",
                table: "MoveRequests",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MoveDateEnd",
                table: "MoveRequests");
        }
    }
}
