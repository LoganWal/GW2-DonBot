using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerFightLogMechanic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CerusOrbsCollected",
                table: "PlayerFightLog");

            migrationBuilder.DropColumn(
                name: "CerusPhaseOneDamage",
                table: "PlayerFightLog");

            migrationBuilder.DropColumn(
                name: "CerusSpreadHitCount",
                table: "PlayerFightLog");

            migrationBuilder.DropColumn(
                name: "DeimosOilsTriggered",
                table: "PlayerFightLog");

            migrationBuilder.DropColumn(
                name: "ShardPickUp",
                table: "PlayerFightLog");

            migrationBuilder.DropColumn(
                name: "ShardUsed",
                table: "PlayerFightLog");

            migrationBuilder.CreateTable(
                name: "PlayerFightLogMechanic",
                columns: table => new
                {
                    PlayerFightLogMechanicId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerFightLogId = table.Column<long>(type: "bigint", nullable: false),
                    MechanicName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MechanicCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerFightLogMechanic", x => x.PlayerFightLogMechanicId);
                    table.ForeignKey(
                        name: "FK_PlayerFightLogMechanic_PlayerFightLog_PlayerFightLogId",
                        column: x => x.PlayerFightLogId,
                        principalTable: "PlayerFightLog",
                        principalColumn: "PlayerFightLogId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerFightLogMechanic_PlayerFightLogId",
                table: "PlayerFightLogMechanic",
                column: "PlayerFightLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerFightLogMechanic");

            migrationBuilder.AddColumn<long>(
                name: "CerusOrbsCollected",
                table: "PlayerFightLog",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "CerusPhaseOneDamage",
                table: "PlayerFightLog",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "CerusSpreadHitCount",
                table: "PlayerFightLog",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "DeimosOilsTriggered",
                table: "PlayerFightLog",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ShardPickUp",
                table: "PlayerFightLog",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ShardUsed",
                table: "PlayerFightLog",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
