using Discord;
using DonBot.Extensions;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;

namespace DonBot.Handlers.GuildWars2Handler.MessageGenerationHandlers;

public sealed class WvWPlayerSummaryHandler(IEntityService entityService, FooterHandler footerHandler)
{
    public async Task<Embed> Generate(Guild gw2Guild)
    {
        var accounts = (await entityService.Account.GetAllAsync()).OrderByDescending(o => o.Points).ToList();
        var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();

        accounts = accounts.Where(s => gw2Accounts.Any(acc => acc.DiscordId == s.DiscordId)).Take(50).ToList();

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
                var accountOverview = "```#    Name                     Points\n";
                var useLimit = false;
                var limit = 0;

                if (position + 20 > accounts.Count)
                {
                    limit = accounts.Count % 20;
                }

                var playerBatch = accounts.Take(new Range(20 * i, !useLimit ? 20 * i + 20 : limit)).ToList();

                if (!playerBatch.Any())
                {
                    break;
                }

                foreach (var account in playerBatch)
                {
                    var gw2Account = gw2Accounts.FirstOrDefault(s => s.DiscordId == account.DiscordId);
                    var points = account.Points;
                    var name = gw2Account?.GuildWarsAccountName ?? $"Unknown - {account.DiscordId}";
                    var pointsDiff = Math.Round(account.Points - account.PreviousPoints);

                    accountOverview += $"{position.ToString().PadLeft(3, '0')}{string.Empty,2}{name.ClipAt(23),-23}{string.Empty,2}{Convert.ToInt32(points)}(+{Convert.ToInt32(pointsDiff)})\n";
                    position++;
                }

                accountOverview += "```";

                message.AddField(x =>
                {
                    x.Name = "Total Points";
                    x.Value = $"{accountOverview}";
                    x.IsInline = false;
                });
            }

            message.Footer = new EmbedFooterBuilder()
            {
                Text = $"{await footerHandler.Generate(gw2Guild.GuildId)}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            };

            // Timestamp
            message.Timestamp = DateTime.Now;
        }

        // Building the message for use
        return message.Build();
    }

    public async Task<Embed> GenerateActive(Guild gw2Guild, string fightLogUrl)
    {
        var accounts = await entityService.Account.GetAllAsync();
        accounts = accounts.Where(account => (account.Points - account.PreviousPoints) > 0)
            .OrderByDescending(account => account.Points - account.PreviousPoints)
            .ToList();

        var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();
        accounts = accounts.Where(s => gw2Accounts.Any(acc => acc.DiscordId == s.DiscordId)).ToList();

        var position = 1;

        var message = new EmbedBuilder
        {
            Title = "Report - WvW points\n",
            Description = "**WvW Last fight points:**\n",
            Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
            Author = new EmbedAuthorBuilder()
            {
                Name = "GW2-DonBot",
                Url = "https://github.com/LoganWal/GW2-DonBot",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Url = $"{fightLogUrl}"
        };

        if (gw2Guild.DiscordGuildMemberRoleId != null)
        {
            for (var i = 0; i < accounts.Count / 20 + 1; i++)
            {
                var accountOverview = "```#    Name                     Points\n";
                var useLimit = false;
                var limit = 0;

                if (position + 20 > accounts.Count)
                {
                    limit = accounts.Count % 20;
                }

                var playerBatch = accounts.Take(new Range(20 * i, !useLimit ? 20 * i + 20 : limit)).ToList();

                if (!playerBatch.Any())
                {
                    break;
                }

                foreach (var account in playerBatch)
                {
                    var gw2Account = gw2Accounts.FirstOrDefault(s => s.DiscordId == account.DiscordId);
                    var name = gw2Account?.GuildWarsAccountName ?? $"Unknown - {account.DiscordId}";
                    var pointsDiff = Math.Round(account.Points - account.PreviousPoints);

                    accountOverview += $"{position.ToString().PadLeft(3, '0')}  {name.ClipAt(23),-23}  (+{Convert.ToInt32(pointsDiff)})\n";
                    position++;
                }

                accountOverview += "```";

                message.AddField(x =>
                {
                    x.Name = "Latest Fight Points";
                    x.Value = $"{accountOverview}";
                    x.IsInline = false;
                });
            }

            message.Footer = new EmbedFooterBuilder()
            {
                Text = $"{await footerHandler.Generate(gw2Guild.GuildId)}",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            };

            // Timestamp
            message.Timestamp = DateTime.Now;
        }

        // Building the message for use
        return message.Build();
    }
}