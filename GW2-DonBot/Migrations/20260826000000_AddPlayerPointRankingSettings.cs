using DonBot.Core.Models.Entities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DatabaseContext))]
    [Migration("20260826000000_AddPlayerPointRankingSettings")]
    public partial class AddPlayerPointRankingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PlayerPointRankingsChannelId",
                table: "Guild",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PlayerPointRankingsEnabled",
                table: "Guild",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayerPointRankingsChannelId",
                table: "Guild");

            migrationBuilder.DropColumn(
                name: "PlayerPointRankingsEnabled",
                table: "Guild");
        }
    }
}
