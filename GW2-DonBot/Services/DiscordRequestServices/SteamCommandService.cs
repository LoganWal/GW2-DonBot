using Discord.WebSocket;
using Models.Entities;

namespace Services.DiscordRequestServices
{
    public class SteamCommandService : ISteamCommandService
    {
        private readonly DatabaseContext _databaseContext;

        public SteamCommandService(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        public async Task VerifySteamAccount(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            if (long.TryParse(command.Data.Options.First().Value.ToString(), out var steamId))
            {
                switch (steamId)
                {
                    case 0:
                        await command.FollowupAsync("Please try again and enter a valid steamId account number.", ephemeral: true);
                        break;
                    // if steamId3
                    case <= 76561197960265728:
                        await command.FollowupAsync("Please try again and enter a valid steamId64 account number.", ephemeral: true);
                        break;
                    // assume steamId64
                    default:
                    {
                        var steamAccount = new SteamAccount
                        {
                            SteamId64 = steamId,
                            SteamId3 = steamId - 76561197960265728,
                            DiscordId = (long)command.User.Id
                        };

                        if (_databaseContext.SteamAccount.FirstOrDefault(g => g.SteamId64 == steamAccount.SteamId64) != null)
                        {
                            await command.FollowupAsync("This steam account id is already registered", ephemeral: true);
                            return;
                        }

                        await _databaseContext.AddAsync(steamAccount);
                        await _databaseContext.SaveChangesAsync();

                        await command.FollowupAsync("Registered!", ephemeral: true);
                        break;
                    }
                }
            }
            else
            {
                await command.FollowupAsync("Invalid steam id", ephemeral: true);
            }
        }
    }
}
