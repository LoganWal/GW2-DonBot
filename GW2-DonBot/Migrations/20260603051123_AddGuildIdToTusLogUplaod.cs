using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildIdToTusLogUplaod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GuildId",
                table: "LogUpload",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuildId",
                table: "LogUpload");
        }
    }
}
