using Discord;
using Extensions;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Models.GW2Api;

namespace Handlers.MessageGenerationHandlers
{
    public class WvWPlayerReportHandler
    {
        private readonly DatabaseContext _databaseContext;

        private readonly FooterHandler _footerHandler;

        public WvWPlayerReportHandler(DatabaseContext databaseContext, FooterHandler footerHandler)
        {
            _databaseContext = databaseContext;
            _footerHandler = footerHandler;
        }

        public async Task<Embed> Generate(Guild guildConfiguration)
        {
            var accounts = await _databaseContext.Account.ToListAsync();
            var gw2Accounts = await _databaseContext.GuildWarsAccount.ToListAsync();
            gw2Accounts = gw2Accounts.Where(guildWarsAccount => guildWarsAccount.GuildWarsGuilds?.Split(',', StringSplitOptions.TrimEntries).Contains(guildConfiguration.Gw2GuildMemberRoleId) ?? false).ToList();

            var position = 1;
            var accountsPerMessage = 15;

            var message = new EmbedBuilder
            {
                Title = "WvW Player Report\n",
                Description = "**WvW player report:**\n",
                Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Footer = new EmbedFooterBuilder()
                {
                    Text = $"{_footerHandler.Generate()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Timestamp = DateTime.Now
            };

            for (var i = 0; i < gw2Accounts.Count / accountsPerMessage + 1; i++)
            {
                var accountOverview = "```";
                var useLimit = false;
                var limit = 0;

                if (position + accountsPerMessage > gw2Accounts.Count)
                {
                    limit = gw2Accounts.Count % accountsPerMessage;
                }

                var playerBatch = gw2Accounts.OrderByDescending(o => o.World).Take(new Range(accountsPerMessage * i, !useLimit ? accountsPerMessage * i + accountsPerMessage : limit)).ToList();

                if (!playerBatch.Any())
                {
                    break;
                }

                foreach (var gw2Account in playerBatch)
                {
                    var name = gw2Account.GuildWarsAccountName ?? gw2Account.DiscordId.ToString();
                    var server = ((GW2WorldEnum)gw2Account.World).ToString();
                    var account = accounts.FirstOrDefault(s => s.DiscordId == gw2Account.DiscordId);

                    var lastLogDateTime = "Never";

                    if (account != null)
                    {
                        lastLogDateTime = account.LastWvwLogDateTime.HasValue ? account.LastWvwLogDateTime.Value.AddHours(10).ToString("yyyy-MM-dd") : "Never";
                    }

                    accountOverview += $"{name.ClipAt(34),-34} {server.ClipAt(7),-7}   {lastLogDateTime.ClipAt(12),-12} \n";
                    position++;
                }

                accountOverview += "```";

                message.AddField(x =>
                {
                    x.Name = "``` Name                                    Server     Last Log (AEST)   ```\n";
                    x.Value = $"{accountOverview}";
                    x.IsInline = false;
                });
            }

            return message.Build();
        }
    }
}
