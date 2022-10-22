using Microsoft.EntityFrameworkCore;
using Services.SecretsServices;

namespace Models.Entities
{
    public class DatabaseContext : DbContext
    {
        private ISecretService _secretService;

        public DbSet<Account> Account { get; set; }

        public string DatabasePath { get; }

        public DatabaseContext SetSecretService(ISecretService secretService)
        {
            _secretService = secretService;
            return this;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connection = _secretService.Fetch<string>(nameof(BotSecretsDataModel.SqlServerConnection));
            optionsBuilder.UseSqlServer(connection);
        }
    }
}
