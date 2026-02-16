using Discord;
using DonBot.Extensions;
using DonBot.Models.Apis.GuildWars2Api;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;

namespace DonBot.Handlers.GuildWars2Handler.MessageGenerationHandlers;

public sealed class WvWPlayerReportHandler(IEntityService entityService, FooterHandler footerHandler)
{
    public async Task<Embed> Generate(Guild guildConfiguration)
    {
        var accounts = await entityService.Account.GetAllAsync();
        var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();
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
                Text = $"{await footerHandler.Generate(guildConfiguration.GuildId)}",
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
                var server = ((GuildWars2WorldEnum)gw2Account.Item1.World).ToString();

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