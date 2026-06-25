using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledMessageDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledMessageDelete",
                columns: table => new
                {
                    ScheduledMessageDeleteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeleteAfterUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledMessageDelete", x => x.ScheduledMessageDeleteId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessageDelete_DeleteAfterUtc",
                table: "ScheduledMessageDelete",
                column: "DeleteAfterUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledMessageDelete");
        }
    }
}
