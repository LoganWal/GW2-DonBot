using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledEventPostedEventTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PostedEventTime",
                table: "ScheduledEvent",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"ScheduledEvent\" SET \"PostedEventTime\" = \"UtcEventTime\" - (\"RepeatIntervalDays\" * INTERVAL '1 day') WHERE \"MessageId\" IS NOT NULL AND \"RepeatIntervalDays\" > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PostedEventTime",
                table: "ScheduledEvent");
        }
    }
}
