using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DonBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Account",
                columns: table => new
                {
                    DiscordId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Points = table.Column<decimal>(type: "numeric(16,3)", precision: 16, scale: 3, nullable: false),
                    PreviousPoints = table.Column<decimal>(type: "numeric(16,3)", precision: 16, scale: 3, nullable: false),
                    AvailablePoints = table.Column<decimal>(type: "numeric(16,3)", precision: 16, scale: 3, nullable: false),
                    LastWvwLogDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Account", x => x.DiscordId);
                });

            migrationBuilder.CreateTable(
                name: "FightLog",
                columns: table => new
                {
                    FightLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FightType = table.Column<short>(type: "smallint", nullable: false),
                    FightStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FightDurationInMs = table.Column<long>(type: "bigint", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    FightPercent = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    FightPhase = table.Column<int>(type: "integer", nullable: true),
                    FightMode = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FightLog", x => x.FightLogId);
                });

            migrationBuilder.CreateTable(
                name: "FightsReport",
                columns: table => new
                {
                    FightsReportId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    FightsStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FightsEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FightsReport", x => x.FightsReportId);
                });

            migrationBuilder.CreateTable(
                name: "Guild",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogDropOffChannelId = table.Column<long>(type: "bigint", nullable: true),
                    DiscordGuildMemberRoleId = table.Column<long>(type: "bigint", nullable: true),
                    DiscordSecondaryMemberRoleId = table.Column<long>(type: "bigint", nullable: true),
                    DiscordVerifiedRoleId = table.Column<long>(type: "bigint", nullable: true),
                    Gw2GuildMemberRoleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Gw2SecondaryMemberRoleIds = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AnnouncementChannelId = table.Column<long>(type: "bigint", nullable: true),
                    LogReportChannelId = table.Column<long>(type: "bigint", nullable: true),
                    AdvanceLogReportChannelId = table.Column<long>(type: "bigint", nullable: true),
                    StreamLogChannelId = table.Column<long>(type: "bigint", nullable: true),
                    RaidAlertEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RaidAlertChannelId = table.Column<long>(type: "bigint", nullable: true),
                    RemoveSpamEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RemovedMessageChannelId = table.Column<long>(type: "bigint", nullable: true),
                    AutoSubmitToWingman = table.Column<bool>(type: "boolean", nullable: false),
                    AutoAggregateLogs = table.Column<bool>(type: "boolean", nullable: false),
                    AutoReplySingleLog = table.Column<bool>(type: "boolean", nullable: false),
                    WvwLeaderboardEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WvwLeaderboardChannelId = table.Column<long>(type: "bigint", nullable: true),
                    PveLeaderboardEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PveLeaderboardChannelId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guild", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "GuildQuote",
                columns: table => new
                {
                    GuildQuoteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    Quote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildQuote", x => x.GuildQuoteId);
                });

            migrationBuilder.CreateTable(
                name: "GuildWarsAccount",
                columns: table => new
                {
                    GuildWarsAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordId = table.Column<long>(type: "bigint", nullable: false),
                    GuildWarsApiKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GuildWarsAccountName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GuildWarsGuilds = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    World = table.Column<int>(type: "integer", nullable: false),
                    FailedApiPullCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildWarsAccount", x => x.GuildWarsAccountId);
                });

            migrationBuilder.CreateTable(
                name: "PlayerFightLog",
                columns: table => new
                {
                    PlayerFightLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FightLogId = table.Column<long>(type: "bigint", nullable: false),
                    GuildWarsAccountName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Damage = table.Column<long>(type: "bigint", nullable: false),
                    Cleave = table.Column<long>(type: "bigint", nullable: false),
                    Kills = table.Column<long>(type: "bigint", nullable: false),
                    Deaths = table.Column<long>(type: "bigint", nullable: false),
                    Downs = table.Column<long>(type: "bigint", nullable: false),
                    QuicknessDuration = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    AlacDuration = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    SubGroup = table.Column<long>(type: "bigint", nullable: false),
                    DamageDownContribution = table.Column<long>(type: "bigint", nullable: false),
                    Cleanses = table.Column<long>(type: "bigint", nullable: false),
                    Strips = table.Column<long>(type: "bigint", nullable: false),
                    StabGenOnGroup = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    StabGenOffGroup = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Healing = table.Column<long>(type: "bigint", nullable: false),
                    BarrierGenerated = table.Column<long>(type: "bigint", nullable: false),
                    DistanceFromTag = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: false),
                    TimesDowned = table.Column<int>(type: "integer", nullable: false),
                    Interrupts = table.Column<long>(type: "bigint", nullable: false),
                    TimesInterrupted = table.Column<long>(type: "bigint", nullable: false),
                    NumberOfHitsWhileBlinded = table.Column<long>(type: "bigint", nullable: false),
                    NumberOfMissesAgainst = table.Column<long>(type: "bigint", nullable: false),
                    NumberOfTimesBlockedAttack = table.Column<long>(type: "bigint", nullable: false),
                    NumberOfTimesEnemyBlockedAttack = table.Column<long>(type: "bigint", nullable: false),
                    NumberOfBoonsRipped = table.Column<long>(type: "bigint", nullable: false),
                    DamageTaken = table.Column<long>(type: "bigint", nullable: false),
                    BarrierMitigation = table.Column<long>(type: "bigint", nullable: false),
                    CerusOrbsCollected = table.Column<long>(type: "bigint", nullable: false),
                    CerusSpreadHitCount = table.Column<long>(type: "bigint", nullable: false),
                    CerusPhaseOneDamage = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    DeimosOilsTriggered = table.Column<long>(type: "bigint", nullable: false),
                    ResurrectionTime = table.Column<int>(type: "integer", nullable: false),
                    ShardPickUp = table.Column<long>(type: "bigint", nullable: false),
                    ShardUsed = table.Column<long>(type: "bigint", nullable: false),
                    TimeOfDeath = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerFightLog", x => x.PlayerFightLogId);
                });

            migrationBuilder.CreateTable(
                name: "PlayerRaffleBid",
                columns: table => new
                {
                    RaffleId = table.Column<int>(type: "integer", nullable: false),
                    DiscordId = table.Column<long>(type: "bigint", nullable: false),
                    PointsSpent = table.Column<decimal>(type: "numeric(16,3)", precision: 16, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRaffleBid", x => new { x.RaffleId, x.DiscordId });
                });

            migrationBuilder.CreateTable(
                name: "Raffle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    RaffleType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Raffle", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RotationAnomaly",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SkillId = table.Column<long>(type: "bigint", nullable: false),
                    SkillName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConsecutiveCasts = table.Column<int>(type: "integer", nullable: false),
                    AverageInterval = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    MaxDeviation = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FightUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RotationAnomaly", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledEvent",
                columns: table => new
                {
                    ScheduledEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    Message = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Day = table.Column<short>(type: "smallint", nullable: false),
                    Hour = table.Column<short>(type: "smallint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: true),
                    UtcEventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<short>(type: "smallint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: true),
                    RepeatIntervalDays = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledEvent", x => x.ScheduledEventId);
                });

            migrationBuilder.CreateTable(
                name: "SteamAccount",
                columns: table => new
                {
                    SteamId64 = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SteamId3 = table.Column<long>(type: "bigint", nullable: false),
                    DiscordId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamAccount", x => x.SteamId64);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Account");

            migrationBuilder.DropTable(
                name: "FightLog");

            migrationBuilder.DropTable(
                name: "FightsReport");

            migrationBuilder.DropTable(
                name: "Guild");

            migrationBuilder.DropTable(
                name: "GuildQuote");

            migrationBuilder.DropTable(
                name: "GuildWarsAccount");

            migrationBuilder.DropTable(
                name: "PlayerFightLog");

            migrationBuilder.DropTable(
                name: "PlayerRaffleBid");

            migrationBuilder.DropTable(
                name: "Raffle");

            migrationBuilder.DropTable(
                name: "RotationAnomaly");

            migrationBuilder.DropTable(
                name: "ScheduledEvent");

            migrationBuilder.DropTable(
                name: "SteamAccount");
        }
    }
}
