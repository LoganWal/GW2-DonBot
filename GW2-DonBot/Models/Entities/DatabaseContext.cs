using Microsoft.EntityFrameworkCore;
using Services.SecretsServices;

namespace Models.Entities
{
    public class DatabaseContext : DbContext
    {
        private readonly ISecretService _secretService;

        public DatabaseContext(ISecretService secretService)
        {
            _secretService = secretService;
        }

        public DbSet<Account> Account { get; set; } = null!;
        public DbSet<Guild> Guild { get; set; } = null!;
        public DbSet<Raffle> Raffle { get; set; } = null!;
        public DbSet<PlayerRaffleBid> PlayerRaffleBid { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlayerRaffleBid>().HasKey(prb => new { prb.RaffleId, prb.DiscordId });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connection = _secretService.FetchDonBotSqlConnectionString();
            optionsBuilder.UseSqlServer(connection);
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }
    }
}
