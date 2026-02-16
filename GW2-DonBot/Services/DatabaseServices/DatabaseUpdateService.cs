using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DonBot.Services.DatabaseServices;

public sealed class DatabaseUpdateService<T>(IDbContextFactory<DatabaseContext> contextFactory) : IDatabaseUpdateService<T> where T : class
{
    private Task<DatabaseContext> GetContextAsync() =>
        contextFactory.CreateDbContextAsync();

    public async Task AddAsync(T entity)
    {
        await using var context = await GetContextAsync();
        var dbSet = context.Set<T>();
        await dbSet.AddAsync(entity);
        await SaveAsync(context);
    }

    public async Task AddRangeAsync(List<T> entities)
    {
        await using var context = await GetContextAsync();
        var dbSet = context.Set<T>();
        await dbSet.AddRangeAsync(entities);
        await SaveAsync(context);
    }

    public async Task UpdateAsync(T entity)
    {
        await using var context = await GetContextAsync();
        var dbSet = context.Set<T>();
        dbSet.Attach(entity);
        context.Entry(entity).State = EntityState.Modified;
        await SaveAsync(context);
    }

    public async Task UpdateRangeAsync(List<T> entities)
    {
        await using var context = await GetContextAsync();
        var dbSet = context.Set<T>();
        foreach (var entity in entities)
        {
            dbSet.Attach(entity);
            context.Entry(entity).State = EntityState.Modified;
        }
        await SaveAsync(context);
    }

    public async Task DeleteAsync(T entity)
    {
        await using var context = await GetContextAsync();
        var dbSet = context.Set<T>();
        dbSet.Remove(entity);
        await SaveAsync(context);
    }

    public async Task DeleteRangeAsync(List<T> entities)
    {
        await using var context = await GetContextAsync();
        var dbSet = context.Set<T>();
        dbSet.RemoveRange(entities);
        await SaveAsync(context);
    }

    public async Task<List<T>> GetAllAsync()
    {
        await using var context = await GetContextAsync();
        var dbSet = context.Set<T>();
        return await dbSet.ToListAsync();
    }

    public async Task<bool> IfAnyAsync(Expression<Func<T, bool>> predicate)
    {
        await using var context = await GetContextAsync();
        var dbSet = context.Set<T>();
        return await dbSet.AnyAsync(predicate);
    }

    public async Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        await using var context = await GetContextAsync();
        var dbSet = context.Set<T>();
        return await dbSet.FirstOrDefaultAsync(predicate);
    }

    public async Task<List<T>> GetWhereAsync(Expression<Func<T, bool>> predicate)
    {
        await using var context = await GetContextAsync();
        var dbSet = context.Set<T>();
        return await dbSet.Where(predicate).ToListAsync();
    }

    private static async Task SaveAsync(DatabaseContext context)
    {
        await context.SaveChangesAsync();
    }
}