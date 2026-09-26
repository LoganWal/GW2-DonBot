using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    public partial class AddRegenerationTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RegenDuration",
                table: "PlayerFightLog",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegenDuration",
                table: "PlayerFightLog");
        }
    }
}
