using DonBot.Models.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Tests.Infrastructure;

/// In-memory SQLite-backed <see cref="DatabaseContext"/> for tests. Each instance owns a
/// dedicated connection so tests stay isolated. Caller must dispose.
internal sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DatabaseContext> _options;

    public SqliteTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new DatabaseContext(_options);
        ctx.Database.EnsureCreated();
    }

    public DatabaseContext NewContext() => new(_options);

    public IDbContextFactory<DatabaseContext> Factory => new SqliteFactory(_options);

    public void Dispose() => _connection.Dispose();

    private sealed class SqliteFactory(DbContextOptions<DatabaseContext> options) : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => new(options);
    }
}
