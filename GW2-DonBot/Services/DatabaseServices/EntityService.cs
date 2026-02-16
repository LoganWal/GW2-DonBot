using DonBot.Models.Entities;

namespace DonBot.Services.DatabaseServices;

public sealed class EntityService(
    IDatabaseUpdateService<Account> account,
    IDatabaseUpdateService<FightLog> fightLog,
    IDatabaseUpdateService<FightsReport> fightsReport,
    IDatabaseUpdateService<Guild> guild,
    IDatabaseUpdateService<GuildQuote> guildQuote,
    IDatabaseUpdateService<GuildWarsAccount> guildWarsAccount,
    IDatabaseUpdateService<PlayerFightLog> playerFightLog,
    IDatabaseUpdateService<PlayerRaffleBid> playerRaffleBid,
    IDatabaseUpdateService<Raffle> raffle,
    IDatabaseUpdateService<SteamAccount> steamAccount,
    IDatabaseUpdateService<ScheduledEvent> scheduledEvent)
    : IEntityService
{
    public IDatabaseUpdateService<Account> Account { get; } = account;

    public IDatabaseUpdateService<FightLog> FightLog { get; } = fightLog;

    public IDatabaseUpdateService<FightsReport> FightsReport { get; } = fightsReport;

    public IDatabaseUpdateService<Guild> Guild { get; } = guild;

    public IDatabaseUpdateService<GuildQuote> GuildQuote { get; } = guildQuote;

    public IDatabaseUpdateService<GuildWarsAccount> GuildWarsAccount { get; } = guildWarsAccount;

    public IDatabaseUpdateService<PlayerFightLog> PlayerFightLog { get; } = playerFightLog;

    public IDatabaseUpdateService<PlayerRaffleBid> PlayerRaffleBid { get; } = playerRaffleBid;

    public IDatabaseUpdateService<Raffle> Raffle { get; } = raffle;

    public IDatabaseUpdateService<SteamAccount> SteamAccount { get; } = steamAccount;

    public IDatabaseUpdateService<ScheduledEvent> ScheduledEvent { get; } = scheduledEvent;
}