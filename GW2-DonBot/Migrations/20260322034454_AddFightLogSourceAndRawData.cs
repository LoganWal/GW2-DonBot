using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class AddFightLogSourceAndRawData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "FightLog",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FightLogRawData",
                columns: table => new
                {
                    FightLogId = table.Column<long>(type: "bigint", nullable: false),
                    RawFightData = table.Column<string>(type: "text", maxLength: 104857600, nullable: true),
                    RawHealingData = table.Column<string>(type: "text", maxLength: 104857600, nullable: true),
                    RawBarrierData = table.Column<string>(type: "text", maxLength: 104857600, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FightLogRawData", x => x.FightLogId);
                    table.ForeignKey(
                        name: "FK_FightLogRawData_FightLog_FightLogId",
                        column: x => x.FightLogId,
                        principalTable: "FightLog",
                        principalColumn: "FightLogId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FightLogRawData");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "FightLog");
        }
    }
}
