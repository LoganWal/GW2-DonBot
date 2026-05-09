using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNonGw2Features : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SteamAccount");

            // EventType 3 was the (now-removed) Wordle entry in ScheduledEventTypeEnum.
            migrationBuilder.Sql("DELETE FROM \"ScheduledEvent\" WHERE \"EventType\" = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SteamAccount",
                columns: table => new
                {
                    SteamId64 = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscordId = table.Column<long>(type: "bigint", nullable: false),
                    SteamId3 = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamAccount", x => x.SteamId64);
                });
        }
    }
}
