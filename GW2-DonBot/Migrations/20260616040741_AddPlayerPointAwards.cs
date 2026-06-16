using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerPointAwards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerPointAward",
                columns: table => new
                {
                    PlayerPointAwardId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FightLogId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerFightLogId = table.Column<long>(type: "bigint", nullable: false),
                    DiscordId = table.Column<long>(type: "bigint", nullable: false),
                    GuildWarsAccountName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FightType = table.Column<short>(type: "smallint", nullable: false),
                    Metric = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MetricLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MetricValue = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: false),
                    PercentileValue = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: false),
                    BasePoints = table.Column<decimal>(type: "numeric(16,3)", precision: 16, scale: 3, nullable: false),
                    Multiplier = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    Points = table.Column<decimal>(type: "numeric(16,3)", precision: 16, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerPointAward", x => x.PlayerPointAwardId);
                    table.ForeignKey(
                        name: "FK_PlayerPointAward_FightLog_FightLogId",
                        column: x => x.FightLogId,
                        principalTable: "FightLog",
                        principalColumn: "FightLogId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerPointAward_PlayerFightLog_PlayerFightLogId",
                        column: x => x.PlayerFightLogId,
                        principalTable: "PlayerFightLog",
                        principalColumn: "PlayerFightLogId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPointAward_FightLogId",
                table: "PlayerPointAward",
                column: "FightLogId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPointAward_PlayerFightLogId_Metric",
                table: "PlayerPointAward",
                columns: new[] { "PlayerFightLogId", "Metric" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerPointAward");
        }
    }
}
