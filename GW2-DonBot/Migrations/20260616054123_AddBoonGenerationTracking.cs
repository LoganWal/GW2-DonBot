using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddBoonGenerationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AlacGenGroup",
                table: "PlayerFightLog",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BoonRole",
                table: "PlayerFightLog",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "QuicknessGenGroup",
                table: "PlayerFightLog",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlacGenGroup",
                table: "PlayerFightLog");

            migrationBuilder.DropColumn(
                name: "BoonRole",
                table: "PlayerFightLog");

            migrationBuilder.DropColumn(
                name: "QuicknessGenGroup",
                table: "PlayerFightLog");
        }
    }
}
