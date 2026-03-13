using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DonBot.Models.Entities;

public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        // Fallback is only used by dotnet ef tooling (migrations add/update) — never at runtime
        var connectionString = Environment.GetEnvironmentVariable("DonBotSqlConnectionString")
            ?? "Host=localhost;Port=5432;Database=DonBot;Username=postgres;Password=postgres;";

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DatabaseContext(options);
    }
}
