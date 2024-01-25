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

        public WvWPlayerReportHandler(IDatabaseContext databaseContext, FooterHandler footerHandler)
        {
            _databaseContext = databaseContext.GetDatabaseContext();
            _footerHandler = footerHandler;
        }

        public async Task<Embed> Generate()
        {
            // TODO: UPDATE THIS TO BE PER GUILD
            var guild = _databaseContext.Guild.First(guild => guild.GuildId == 415441457151737870);
            var accounts = await _databaseContext.Account.ToListAsync();
            accounts = accounts.Where(acc => acc.Guilds?.Split(",", StringSplitOptions.TrimEntries).Contains(guild.Gw2GuildMemberRoleId) ?? false).ToList();
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

            for (var i = 0; i < accounts.Count / accountsPerMessage + 1; i++)
            {
                var accountOverview = "```";
                var useLimit = false;
                var limit = 0;

                if (position + accountsPerMessage > accounts.Count)
                {
                    limit = accounts.Count % accountsPerMessage;
                }

                var playerBatch = accounts.OrderByDescending(o => o.LastWvwLogDateTime).Take(new Range(accountsPerMessage * i, !useLimit ? accountsPerMessage * i + accountsPerMessage : limit)).ToList();

                if (!playerBatch.Any())
                {
                    break;
                }

                foreach (var account in playerBatch)
                {
                    var name = account.Gw2AccountName;
                    var server = "Unknown";
                    if (account.World.HasValue)
                    {
                        server = ((GW2WorldEnum)account.World).ToString();
                    }

                    var lastLogDateTime = account.LastWvwLogDateTime.HasValue ? account.LastWvwLogDateTime.Value.AddHours(10).ToString("yyyy-MM-dd") : "Never";

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
