using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledEventNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastNotificationEventTime",
                table: "ScheduledEvent",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "NotificationMinutesBeforeStart",
                table: "ScheduledEvent",
                type: "smallint",
                nullable: false,
                defaultValue: (short)15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastNotificationEventTime",
                table: "ScheduledEvent");

            migrationBuilder.DropColumn(
                name: "NotificationMinutesBeforeStart",
                table: "ScheduledEvent");
        }
    }
}
