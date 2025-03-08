using DonBot.Models.Apis.DeadlockApi;

namespace DonBot.Services.DeadlockServices
{
    public interface IDeadlockApiService
    {
        Task<DeadlockRank> GetDeadlockRank(long accountId);

        Task<List<DeadlockRankHistory>> GetDeadlockRankHistory(long accountId);
    }
}
