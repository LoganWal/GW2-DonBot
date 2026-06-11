using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledEventResponseOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResponseOptionsJson",
                table: "ScheduledEvent",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE \"ScheduledEvent\" SET \"ResponseOptionsJson\" = '[{\"label\":\"Join\",\"emoji\":\"✅\"},{\"label\":\"Can''t Join\",\"emoji\":\"❌\"},{\"label\":\"Will Be Late\",\"emoji\":\"⏰\"}]' WHERE \"EventType\" = 4;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponseOptionsJson",
                table: "ScheduledEvent");
        }
    }
}
