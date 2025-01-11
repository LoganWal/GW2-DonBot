using System.Linq.Expressions;

namespace DonBot.Services.DatabaseServices
{
    public interface IDatabaseUpdateService<T> where T : class
    {
        Task AddAsync(T entity);

        Task AddRangeAsync(List<T> entity);

        Task UpdateAsync(T entity);

        Task UpdateRangeAsync(List<T> entity);

        Task DeleteAsync(T entity);

        Task DeleteRangeAsync(List<T> entity);

        Task<List<T>> GetAllAsync();

        Task<bool> IfAnyAsync(Expression<Func<T, bool>> predicate);

        Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

        Task<List<T>> GetWhereAsync(Expression<Func<T, bool>> predicate);
    }
}