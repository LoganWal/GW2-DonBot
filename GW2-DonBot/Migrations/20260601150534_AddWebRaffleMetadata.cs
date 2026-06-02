using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddWebRaffleMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatorDiscordId",
                table: "Raffle",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MessageChannelId",
                table: "Raffle",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MessageId",
                table: "Raffle",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatorDiscordId",
                table: "Raffle");

            migrationBuilder.DropColumn(
                name: "MessageChannelId",
                table: "Raffle");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "Raffle");
        }
    }
}
