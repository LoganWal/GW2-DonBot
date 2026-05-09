using DonBot.Models.Entities;

namespace DonBot.Services.DatabaseServices;

public interface IEntityService
{
    IDatabaseUpdateService<Account> Account { get; }

    IDatabaseUpdateService<FightLog> FightLog { get; }

    IDatabaseUpdateService<FightLogRawData> FightLogRawData { get; }

    IDatabaseUpdateService<FightsReport> FightsReport { get; }

    IDatabaseUpdateService<Guild> Guild { get; }

    IDatabaseUpdateService<GuildQuote> GuildQuote { get; }

    IDatabaseUpdateService<GuildWarsAccount> GuildWarsAccount { get; }

    IDatabaseUpdateService<PlayerFightLog> PlayerFightLog { get; }

    IDatabaseUpdateService<PlayerFightLogMechanic> PlayerFightLogMechanic { get; }

    IDatabaseUpdateService<PlayerRaffleBid> PlayerRaffleBid { get; }

    IDatabaseUpdateService<Raffle> Raffle { get; }

    IDatabaseUpdateService<ScheduledEvent> ScheduledEvent { get; }

    IDatabaseUpdateService<RotationAnomaly> RotationAnomaly { get; }
}