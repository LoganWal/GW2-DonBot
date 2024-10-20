using Models.DeadlockApi;

namespace Services.DiscordApiServices
{
    public interface IDeadlockApiService
    {
        Task<DeadlockRank> GetDeadlockRank(long accountId);

        Task<List<DeadlockRankHistory>> GetDeadlockRankHistory(long accountId);
    }
}
