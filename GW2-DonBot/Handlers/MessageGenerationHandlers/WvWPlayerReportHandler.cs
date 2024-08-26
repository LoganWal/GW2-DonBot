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
            var gw2AccountData = gw2Accounts.Select(g => new Tuple<GuildWarsAccount, Account?>(g, accounts.FirstOrDefault(s => s.DiscordId == g.DiscordId))).ToList();

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
                    Text = $"{_footerHandler.Generate(guildConfiguration.GuildId)}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Timestamp = DateTime.Now
            };

            for (var i = 0; i < gw2AccountData.Count / accountsPerMessage + 1; i++)
            {
                var accountOverview = "```Name                 World   Last Log\n";
                var useLimit = false;
                var limit = 0;

                if (position + accountsPerMessage > gw2AccountData.Count)
                {
                    limit = gw2AccountData.Count % accountsPerMessage;
                }

                var playerBatch = gw2AccountData.OrderByDescending(o => o.Item2?.LastWvwLogDateTime).Take(new Range(accountsPerMessage * i, !useLimit ? accountsPerMessage * i + accountsPerMessage : limit)).ToList();

                if (!playerBatch.Any())
                {
                    break;
                }

                foreach (var gw2Account in playerBatch)
                {
                    var name = gw2Account.Item1.GuildWarsAccountName ?? gw2Account.Item1.DiscordId.ToString();
                    var server = ((Gw2WorldEnum)gw2Account.Item1.World).ToString();

                    var lastLogDateTime = "Never";

                    if (gw2Account.Item2 != null)
                    {
                        lastLogDateTime = gw2Account.Item2.LastWvwLogDateTime.HasValue ? gw2Account.Item2.LastWvwLogDateTime.Value.AddHours(10).ToString("yyyy-MM-dd") : "Never";
                    }

                    accountOverview += $"{name.ClipAt(20),-20} {server.ClipAt(7),-7} {lastLogDateTime.ClipAt(10),-10}\n";
                    position++;
                }

                accountOverview += "```";

                message.AddField(x =>
                {
                    x.Name = "Guild Players";
                    x.Value = $"{accountOverview}";
                    x.IsInline = false;
                });
            }

            return message.Build();
        }
    }
}
