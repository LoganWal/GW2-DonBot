using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddTusFileIdToLogUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TusFileId",
                table: "LogUpload",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogUpload_TusFileId",
                table: "LogUpload",
                column: "TusFileId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LogUpload_TusFileId",
                table: "LogUpload");

            migrationBuilder.DropColumn(
                name: "TusFileId",
                table: "LogUpload");
        }
    }
}
