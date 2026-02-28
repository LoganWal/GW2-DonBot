using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.GuildWars2;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices;
using Microsoft.Extensions.Logging;
using static System.Text.RegularExpressions.Regex;

namespace DonBot.Services.DiscordServices;

public class DiscordMessageHandler(
    ILogger<DiscordMessageHandler> logger,
    IEntityService entityService,
    IMessageGenerationService messageGenerationService,
    IPlayerService playerService,
    IDataModelGenerationService dataModelGenerator,
    DiscordSocketClient client)
{
    private const long DonBotId = 1021682849797111838;
    private readonly HashSet<string> _seenUrls = [];

    public async Task LoadExistingFightLogs()
    {
        var fightLogs = await entityService.FightLog.GetAllAsync();
        foreach (var url in fightLogs.Select(s => s.Url).Distinct())
        {
            _seenUrls.Add(url);
        }
    }

    public Task MessageReceivedAsync(SocketMessage seenMessage)
    {
        _ = HandleMessage(seenMessage);
        return Task.CompletedTask;
    }

    private async Task HandleMessage(SocketMessage seenMessage)
    {
        try
        {
            // TODO update this to be config driven
            ulong[] knownBots =
            [
                DonBotId,
                1172050606005964820, // gw2Mists.com
                1408608200424554507  // gw2SoxBot
            ];

            if (knownBots.Contains(seenMessage.Author.Id))
            {
                return;
            }

            Guild? guild;
            if (seenMessage.Channel is not SocketGuildChannel channel)
            {
                logger.LogWarning("Did not find channel {SeenMessageChannelName} in guild", seenMessage.Channel.Name);
                guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == -1);
            }
            else
            {
                guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)channel.Guild.Id);
            }

            if ((guild?.RemoveSpamEnabled ?? false) && IsMatch(seenMessage.Content, @"\b((https?|ftp)://|www\.|(\w+\.)+\w{2,})(\S*)\b"))
            {
                if (seenMessage.Channel is not SocketTextChannel)
                {
                    logger.LogWarning("Unable to spam channel {SeenMessageChannelName}", seenMessage.Channel.Name);
                    return;
                }

                var user = await entityService.Account.GetFirstOrDefaultAsync(g => g.DiscordId == (long)seenMessage.Author.Id);
                if (user is null)
                {
                    await HandleSpamMessage(seenMessage);
                    return;
                }

                if (guild.DiscordVerifiedRoleId is not null)
                {
                    if (seenMessage.Author is SocketGuildUser socketUser &&
                        !socketUser.Roles.Select(s => (long)s.Id).Contains(guild.DiscordVerifiedRoleId.Value))
                    {
                        await HandleSpamMessage(seenMessage);
                        return;
                    }
                }
            }

            bool embedMessage;
            List<string> trimmedUrls;
            if (seenMessage.Source != MessageSource.Webhook || seenMessage.Channel.Id != (ulong)(guild?.LogDropOffChannelId ?? -1))
            {
                embedMessage = false;

                const string pattern = @"https://(?:b\.dps|wvw|dps)\.report/\S+";
                var matches = Matches(seenMessage.Content, pattern);
                trimmedUrls = matches.Select(match => match.Value).ToList();

                const string wingmanPattern = @"https://gw2wingman\.nevermindcreations\.de/log/\S+";
                matches = Matches(seenMessage.Content, wingmanPattern);

                var wingmanMatches = matches.Select(match => match.Value).ToList();
                for (var i = 0; i < wingmanMatches.Count; i++)
                {
                    wingmanMatches[i] = wingmanMatches[i].Replace("https://gw2wingman.nevermindcreations.de/log/", "https://gw2wingman.nevermindcreations.de/logContent/");
                }

                trimmedUrls.AddRange(wingmanMatches);
            }
            else
            {
                embedMessage = true;

                var urls = seenMessage.Embeds.SelectMany(x => x.Fields.SelectMany(y => y.Value.Split('('))).Where(x => x.Contains(")")).ToList();
                urls.AddRange(seenMessage.Embeds.Select(x => x.Url).Where(x => !string.IsNullOrEmpty(x)));

                trimmedUrls = urls.Select(url => url.Contains(')') ? url[..url.IndexOf(')')] : url).ToList();
            }

            if (trimmedUrls.Count != 0)
            {
                logger.LogInformation("Assessing: {Url}", string.Join(",", trimmedUrls));
                await AnalyseAndReportOnUrl(trimmedUrls, guild?.GuildId ?? -1, embedMessage, seenMessage.Channel);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to handle message");
        }
    }

    private async Task HandleSpamMessage(SocketMessage seenMessage)
    {
        await seenMessage.DeleteAsync();

        var discordGuild = (seenMessage.Channel as SocketGuildChannel)?.Guild;
        if (discordGuild is null)
        {
            return;
        }

        var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)discordGuild.Id);
        if (guild?.RemovedMessageChannelId is null)
        {
            logger.LogWarning("Unable to find guild {GuildId}", discordGuild.Id);
            return;
        }

        if (client.GetChannel((ulong)guild.RemovedMessageChannelId) is not ITextChannel targetChannel)
        {
            logger.LogWarning("Unable to find guild remove channel {GuildRemovedMessageChannelId}", guild.RemovedMessageChannelId);
            return;
        }

        await targetChannel.SendMessageAsync($"Removed message from <@{seenMessage.Author.Id}> ({seenMessage.Author.Username}), for posting a discord link without being verified.");
    }

    private async Task AnalyseAndReportOnUrl(List<string> urls, long guildId, bool isEmbed, ISocketMessageChannel replyChannel)
    {
        var urlList = string.Join(",", urls);
        if (isEmbed && urls.All(url => _seenUrls.Contains(url)))
        {
            logger.LogWarning("Already seen, not analysing or reporting: {url}", urlList);
            return;
        }

        logger.LogInformation("Analysing and reporting on: {url}", urlList);
        var dataList = new List<EliteInsightDataModel>();

        foreach (var url in urls)
        {
            dataList.Add(await dataModelGenerator.GenerateEliteInsightDataModelFromUrl(url));
        }

        var guilds = await entityService.Guild.GetAllAsync();
        var guild = guilds.FirstOrDefault(g => g.GuildId == guildId) ?? guilds.Single(s => s.GuildId == -1);

        logger.LogInformation("Generating fight summary: {url}", urlList);

        MessageComponent? buttonBuilder = null;
        if (isEmbed)
        {
            foreach (var eliteInsightDataModel in dataList)
            {
                if (_seenUrls.Contains(eliteInsightDataModel.FightEliteInsightDataModel.Url))
                {
                    logger.LogInformation("Already seen {url}, going to next log.", eliteInsightDataModel.FightEliteInsightDataModel.Url);
                    continue;
                }

                Embed message;
                if (eliteInsightDataModel.FightEliteInsightDataModel.Wvw)
                {
                    if (guild.AdvanceLogReportChannelId != null)
                    {
                        if (client.GetChannel((ulong)guild.AdvanceLogReportChannelId) is not ITextChannel advanceLogReportChannel)
                        {
                            logger.LogWarning("Failed to find the target channel {guildAdvanceLogReportChannelId}", guild.AdvanceLogReportChannelId);
                            await replyChannel.SendMessageAsync("Failed to find the advanced log report channel.");
                            continue;
                        }

                        var advancedMessage = await messageGenerationService.GenerateWvWFightSummary(eliteInsightDataModel, true, guild, client);
                        await advanceLogReportChannel.SendMessageAsync(text: "", embeds: [advancedMessage]);
                    }

                    if (guild.PlayerReportChannelId != null && client.GetChannel((ulong)guild.PlayerReportChannelId) is SocketTextChannel playerChannel)
                    {
                        var messages = await playerChannel.GetMessagesAsync(10).FlattenAsync();
                        var recentMessages = messages.Where(m => (DateTimeOffset.UtcNow - m.CreatedAt).TotalDays < 14).ToList();
                        if (recentMessages.Count > 0)
                        {
                            await playerChannel.DeleteMessagesAsync(recentMessages);
                        }

                        var activePlayerMessage = await messageGenerationService.GenerateWvWActivePlayerSummary(guild, eliteInsightDataModel.FightEliteInsightDataModel.Url);
                        var playerMessage = await messageGenerationService.GenerateWvWPlayerSummary(guild);

                        await playerChannel.SendMessageAsync(text: "", embeds: [activePlayerMessage]);
                        await playerChannel.SendMessageAsync(text: "", embeds: [playerMessage]);
                    }

                    await playerService.SetPlayerPoints(eliteInsightDataModel);

                    message = await messageGenerationService.GenerateWvWFightSummary(eliteInsightDataModel, false, guild, client);
                    buttonBuilder = new ComponentBuilder()
                        .WithButton("Know My Enemy", ButtonId.KnowMyEnemy)
                        .Build();
                }
                else
                {
                    message = await messageGenerationService.GeneratePvEFightSummary(eliteInsightDataModel, guildId);
                }

                if (guild.LogReportChannelId == null)
                {
                    logger.LogWarning("no log report channel id for guild id `{guildId}`", guild.GuildId);
                    return;
                }

                if (client.GetChannel((ulong)guild.LogReportChannelId) is not ITextChannel logReportChannel)
                {
                    logger.LogWarning("Failed to find the target channel {guildLogReportChannelId}", guild.LogReportChannelId);
                    return;
                }

                await logReportChannel.SendMessageAsync(text: "", embeds: [message], components: buttonBuilder);
            }
        }
        else
        {
            if (dataList.Count > 1)
            {
                foreach (var eliteInsightDataModel in dataList)
                {
                    if (eliteInsightDataModel.FightEliteInsightDataModel.Wvw)
                    {
                        await messageGenerationService.GenerateWvWFightSummary(eliteInsightDataModel, false, guild, client);
                    }
                    else
                    {
                        await messageGenerationService.GeneratePvEFightSummary(eliteInsightDataModel, guildId);
                    }
                }

                var messages = await messageGenerationService.GenerateRaidReplyReport(urls, guildId);
                if (messages != null)
                {
                    var firstPveMessage = messages.FirstOrDefault(m => m.Title?.Contains("PvE") == true);
                    MessageComponent? bestTimesComponent = null;
                    if (firstPveMessage != null)
                    {
                        var urlFights = await entityService.FightLog.GetWhereAsync(s =>
                            urls.Contains(s.Url) &&
                            s.FightType != (short)FightTypesEnum.WvW &&
                            s.FightType != (short)FightTypesEnum.Unkn);
                        if (urlFights.Any())
                        {
                            var fightsReport = new FightsReport
                            {
                                GuildId = guildId,
                                FightsStart = urlFights.Min(f => f.FightStart),
                                FightsEnd = urlFights.Max(f => f.FightStart.AddMilliseconds(f.FightDurationInMs))
                            };
                            await entityService.FightsReport.AddAsync(fightsReport);
                            bestTimesComponent = new ComponentBuilder()
                                .WithButton("Best Times", $"{ButtonId.BestTimesPvEPrefix}{fightsReport.FightsReportId}")
                                .Build();
                        }
                    }

                    foreach (var bulkMessage in messages)
                    {
                        var components = bulkMessage == firstPveMessage ? bestTimesComponent : null;
                        await replyChannel.SendMessageAsync(embeds: [bulkMessage], components: components);
                    }
                }
            }
        }

        logger.LogInformation("Completed and posted report on: {url}", urlList);
        foreach (var url in urls)
        {
            _seenUrls.Add(url);
        }
    }
}
