using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFailedApiPullCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedApiPullCount",
                table: "GuildWarsAccount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedApiPullCount",
                table: "GuildWarsAccount",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
