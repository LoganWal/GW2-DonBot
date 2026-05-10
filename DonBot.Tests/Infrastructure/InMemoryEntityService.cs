using System.Linq.Expressions;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;

namespace DonBot.Tests.Infrastructure;

/// In-memory <see cref="IEntityService"/> for tests that don't need real DB semantics.
/// Each repo is an in-memory list with simple Add/Update/Delete/GetWhere implementations.
internal sealed class InMemoryEntityService : IEntityService
{
    public InMemoryRepo<Account> AccountRepo { get; } = new();
    public InMemoryRepo<FightLog> FightLogRepo { get; } = new();
    public InMemoryRepo<FightLogRawData> FightLogRawDataRepo { get; } = new();
    public InMemoryRepo<FightsReport> FightsReportRepo { get; } = new();
    public InMemoryRepo<Guild> GuildRepo { get; } = new();
    public InMemoryRepo<GuildQuote> GuildQuoteRepo { get; } = new();
    public InMemoryRepo<GuildWarsAccount> GuildWarsAccountRepo { get; } = new();
    public InMemoryRepo<PlayerFightLog> PlayerFightLogRepo { get; } = new();
    public InMemoryRepo<PlayerFightLogMechanic> PlayerFightLogMechanicRepo { get; } = new();
    public InMemoryRepo<PlayerRaffleBid> PlayerRaffleBidRepo { get; } = new();
    public InMemoryRepo<Raffle> RaffleRepo { get; } = new();
    public InMemoryRepo<ScheduledEvent> ScheduledEventRepo { get; } = new();
    public InMemoryRepo<RotationAnomaly> RotationAnomalyRepo { get; } = new();

    public IDatabaseUpdateService<Account> Account => AccountRepo;
    public IDatabaseUpdateService<FightLog> FightLog => FightLogRepo;
    public IDatabaseUpdateService<FightLogRawData> FightLogRawData => FightLogRawDataRepo;
    public IDatabaseUpdateService<FightsReport> FightsReport => FightsReportRepo;
    public IDatabaseUpdateService<Guild> Guild => GuildRepo;
    public IDatabaseUpdateService<GuildQuote> GuildQuote => GuildQuoteRepo;
    public IDatabaseUpdateService<GuildWarsAccount> GuildWarsAccount => GuildWarsAccountRepo;
    public IDatabaseUpdateService<PlayerFightLog> PlayerFightLog => PlayerFightLogRepo;
    public IDatabaseUpdateService<PlayerFightLogMechanic> PlayerFightLogMechanic => PlayerFightLogMechanicRepo;
    public IDatabaseUpdateService<PlayerRaffleBid> PlayerRaffleBid => PlayerRaffleBidRepo;
    public IDatabaseUpdateService<Raffle> Raffle => RaffleRepo;
    public IDatabaseUpdateService<ScheduledEvent> ScheduledEvent => ScheduledEventRepo;
    public IDatabaseUpdateService<RotationAnomaly> RotationAnomaly => RotationAnomalyRepo;
}

internal sealed class InMemoryRepo<T> : IDatabaseUpdateService<T> where T : class
{
    public List<T> Items { get; } = [];

    public Task AddAsync(T entity)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(List<T> entity)
    {
        Items.AddRange(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T entity) => Task.CompletedTask;
    public Task UpdateRangeAsync(List<T> entity) => Task.CompletedTask;

    public Task DeleteAsync(T entity)
    {
        Items.Remove(entity);
        return Task.CompletedTask;
    }

    public Task DeleteRangeAsync(List<T> entity)
    {
        foreach (var e in entity) {
            Items.Remove(e);
        }
        return Task.CompletedTask;
    }

    public Task<List<T>> GetAllAsync() => Task.FromResult(Items.ToList());

    public Task<bool> IfAnyAsync(Expression<Func<T, bool>> predicate) =>
        Task.FromResult(Items.AsQueryable().Any(predicate));

    public Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate) =>
        Task.FromResult(Items.AsQueryable().FirstOrDefault(predicate));

    public Task<List<T>> GetWhereAsync(Expression<Func<T, bool>> predicate) =>
        Task.FromResult(Items.AsQueryable().Where(predicate).ToList());
}
