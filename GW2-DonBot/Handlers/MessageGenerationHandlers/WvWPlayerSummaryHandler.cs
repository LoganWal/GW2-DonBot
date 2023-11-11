using Discord;
using Extensions;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Handlers.MessageGenerationHandlers
{
    public class WvWPlayerSummaryHandler
    {
        private readonly DatabaseContext _databaseContext;
        
        private readonly FooterHandler _footerHandler;

        public WvWPlayerSummaryHandler(IDatabaseContext databaseContext, FooterHandler footerHandler)
        {
            _databaseContext = databaseContext.GetDatabaseContext();
            _footerHandler = footerHandler;
        }

        public async Task<Embed> Generate(Guild gw2Guild)
        {
            var accounts = await _databaseContext.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
            var position = 1;

            var message = new EmbedBuilder
            {
                Title = "Report - WvW points\n",
                Description = "**WvW player Details:**\n",
                Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
            };

            if (gw2Guild.DiscordGuildMemberRoleId != null)
            {
                for (var i = 0; i < accounts.Count / 20 + 1; i++)
                {
                    var accountOverview = "```";
                    var useLimit = false;
                    var limit = 0;

                    if (position + 20 > accounts.Count)
                    {
                        limit = accounts.Count % 20;
                    }

                    var playerBatch = accounts.OrderByDescending(o => o.Points).Take(new Range(20 * i, !useLimit ? 20 * i + 20 : limit)).ToList();

                    if (!playerBatch.Any())
                    {
                        break;
                    }

                    foreach (var account in playerBatch)
                    {
                        var points = account.Points;
                        var name = account.Gw2AccountName;
                        var pointsDiff = Math.Round(account.Points - account.PreviousPoints);

                        accountOverview += $"{position.ToString().PadLeft(3, '0')}  {name.ClipAt(23),-23}  {Convert.ToInt32(points)}(+{Convert.ToInt32(pointsDiff)})\n";
                        position++;
                    }

                    accountOverview += "```";

                    message.AddField(x =>
                    {
                        x.Name = "``` #     Name                        Points         ```\n";
                        x.Value = $"{accountOverview}";
                        x.IsInline = false;
                    });
                }

                message.Footer = new EmbedFooterBuilder()
                {
                    Text = $"{_footerHandler.Generate()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                };

                // Timestamp
                message.Timestamp = DateTime.Now;
            }

            // Building the message for use
            return message.Build();
        }
    }
}
