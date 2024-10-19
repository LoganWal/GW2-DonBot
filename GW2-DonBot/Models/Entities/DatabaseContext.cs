using Microsoft.EntityFrameworkCore;
using Services.SecretsServices;

namespace Models.Entities
{
    public sealed class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
            Account = Set<Account>();
            Guild = Set<Guild>();
            Raffle = Set<Raffle>();
            PlayerRaffleBid = Set<PlayerRaffleBid>();
            GuildWarsAccount = Set<GuildWarsAccount>();
            FightLog = Set<FightLog>();
            FightsReport = Set<FightsReport>();
            PlayerFightLog = Set<PlayerFightLog>();
            GuildQuote = Set<GuildQuote>();
        }

        public DbSet<Account> Account { get; set; }
        public DbSet<Guild> Guild { get; set; }
        public DbSet<Raffle> Raffle { get; set; }
        public DbSet<PlayerRaffleBid> PlayerRaffleBid { get; set; }
        public DbSet<GuildWarsAccount> GuildWarsAccount { get; set; }
        public DbSet<FightLog> FightLog { get; set; }
        public DbSet<FightsReport> FightsReport { get; set; }
        public DbSet<PlayerFightLog> PlayerFightLog { get; set; }
        public DbSet<GuildQuote> GuildQuote { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlayerRaffleBid>()
                .HasKey(prb => new { prb.RaffleId, prb.DiscordId });

            modelBuilder.Entity<Account>()
                .Property(a => a.AvailablePoints)
                .HasPrecision(16, 3);

            modelBuilder.Entity<Account>()
                .Property(a => a.Points)
                .HasPrecision(16, 3);

            modelBuilder.Entity<Account>()
                .Property(a => a.PreviousPoints)
                .HasPrecision(16, 3);

            modelBuilder.Entity<FightLog>()
                .Property(fl => fl.FightPercent)
                .HasPrecision(6, 2);

            modelBuilder.Entity<PlayerFightLog>()
                .Property(pfl => pfl.AlacDuration)
                .HasPrecision(6, 2);

            modelBuilder.Entity<PlayerFightLog>()
                .Property(pfl => pfl.CerusPhaseOneDamage)
                .HasPrecision(10, 3);

            modelBuilder.Entity<PlayerFightLog>()
                .Property(pfl => pfl.DistanceFromTag)
                .HasPrecision(16, 2);

            modelBuilder.Entity<PlayerFightLog>()
                .Property(pfl => pfl.QuicknessDuration)
                .HasPrecision(6, 2);

            modelBuilder.Entity<PlayerFightLog>()
                .Property(pfl => pfl.StabGenOffGroup)
                .HasPrecision(6, 2);

            modelBuilder.Entity<PlayerFightLog>()
                .Property(pfl => pfl.StabGenOnGroup)
                .HasPrecision(6, 2);

            modelBuilder.Entity<PlayerRaffleBid>()
                .Property(prb => prb.PointsSpent)
                .HasPrecision(16, 3);
        }
    }
}
