using DonBot.Models.Entities;

namespace DonBot.Services.DatabaseServices;

public sealed class EntityService(
    IDatabaseUpdateService<Account> account,
    IDatabaseUpdateService<FightLog> fightLog,
    IDatabaseUpdateService<FightLogRawData> fightLogRawData,
    IDatabaseUpdateService<FightsReport> fightsReport,
    IDatabaseUpdateService<Guild> guild,
    IDatabaseUpdateService<GuildQuote> guildQuote,
    IDatabaseUpdateService<GuildWarsAccount> guildWarsAccount,
    IDatabaseUpdateService<PlayerFightLog> playerFightLog,
    IDatabaseUpdateService<PlayerFightLogMechanic> playerFightLogMechanic,
    IDatabaseUpdateService<PlayerRaffleBid> playerRaffleBid,
    IDatabaseUpdateService<Raffle> raffle,
    IDatabaseUpdateService<ScheduledEvent> scheduledEvent,
    IDatabaseUpdateService<RotationAnomaly> rotationAnomaly)
    : IEntityService
{
    public IDatabaseUpdateService<Account> Account { get; } = account;

    public IDatabaseUpdateService<FightLog> FightLog { get; } = fightLog;

    public IDatabaseUpdateService<FightLogRawData> FightLogRawData { get; } = fightLogRawData;

    public IDatabaseUpdateService<FightsReport> FightsReport { get; } = fightsReport;

    public IDatabaseUpdateService<Guild> Guild { get; } = guild;

    public IDatabaseUpdateService<GuildQuote> GuildQuote { get; } = guildQuote;

    public IDatabaseUpdateService<GuildWarsAccount> GuildWarsAccount { get; } = guildWarsAccount;

    public IDatabaseUpdateService<PlayerFightLog> PlayerFightLog { get; } = playerFightLog;

    public IDatabaseUpdateService<PlayerFightLogMechanic> PlayerFightLogMechanic { get; } = playerFightLogMechanic;

    public IDatabaseUpdateService<PlayerRaffleBid> PlayerRaffleBid { get; } = playerRaffleBid;

    public IDatabaseUpdateService<Raffle> Raffle { get; } = raffle;

    public IDatabaseUpdateService<ScheduledEvent> ScheduledEvent { get; } = scheduledEvent;

    public IDatabaseUpdateService<RotationAnomaly> RotationAnomaly { get; } = rotationAnomaly;
}