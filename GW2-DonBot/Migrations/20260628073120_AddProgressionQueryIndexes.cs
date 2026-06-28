using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressionQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PlayerFightLog_FightLogId",
                table: "PlayerFightLog",
                column: "FightLogId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerFightLog_GuildWarsAccountName_FightLogId",
                table: "PlayerFightLog",
                columns: new[] { "GuildWarsAccountName", "FightLogId" });

            migrationBuilder.CreateIndex(
                name: "IX_FightLog_FightType_FightMode_FightStart",
                table: "FightLog",
                columns: new[] { "FightType", "FightMode", "FightStart" });

            migrationBuilder.CreateIndex(
                name: "IX_FightLog_FightType_FightStart",
                table: "FightLog",
                columns: new[] { "FightType", "FightStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerFightLog_FightLogId",
                table: "PlayerFightLog");

            migrationBuilder.DropIndex(
                name: "IX_PlayerFightLog_GuildWarsAccountName_FightLogId",
                table: "PlayerFightLog");

            migrationBuilder.DropIndex(
                name: "IX_FightLog_FightType_FightMode_FightStart",
                table: "FightLog");

            migrationBuilder.DropIndex(
                name: "IX_FightLog_FightType_FightStart",
                table: "FightLog");
        }
    }
}
