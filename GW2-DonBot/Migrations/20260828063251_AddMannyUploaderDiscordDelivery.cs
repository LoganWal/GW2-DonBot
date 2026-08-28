using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddMannyUploaderDiscordDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiscordDeliveryChannelId",
                table: "LogUpload",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordDeliveryMode",
                table: "LogUpload",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MannyUploaderChannelOverrideEnabled",
                table: "Guild",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MannyUploaderDiscordDeliveryEnabled",
                table: "Guild",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LogUploadDiscordDeliveryReceipt",
                columns: table => new
                {
                    LogUploadDiscordDeliveryReceiptId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogUploadId = table.Column<long>(type: "bigint", nullable: false),
                    MessageKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResolvedChannelId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DiscordMessageId = table.Column<long>(type: "bigint", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogUploadDiscordDeliveryReceipt", x => x.LogUploadDiscordDeliveryReceiptId);
                    table.ForeignKey(
                        name: "FK_LogUploadDiscordDeliveryReceipt_LogUpload_LogUploadId",
                        column: x => x.LogUploadId,
                        principalTable: "LogUpload",
                        principalColumn: "LogUploadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogUploadDiscordDeliveryReceipt_LogUploadId_MessageKind",
                table: "LogUploadDiscordDeliveryReceipt",
                columns: new[] { "LogUploadId", "MessageKind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogUploadDiscordDeliveryReceipt");

            migrationBuilder.DropColumn(
                name: "DiscordDeliveryChannelId",
                table: "LogUpload");

            migrationBuilder.DropColumn(
                name: "DiscordDeliveryMode",
                table: "LogUpload");

            migrationBuilder.DropColumn(
                name: "MannyUploaderChannelOverrideEnabled",
                table: "Guild");

            migrationBuilder.DropColumn(
                name: "MannyUploaderDiscordDeliveryEnabled",
                table: "Guild");
        }
    }
}
