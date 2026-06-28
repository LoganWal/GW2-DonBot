using System.Globalization;
using Discord;
using Discord.WebSocket;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services;
using DonBot.Extensions;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public sealed class WvWFightSummaryService(
    IEntityService entityService,
    IPlayerService playerService,
    IFooterService footerService,
    IDbContextFactory<DatabaseContext> dbContextFactory,
    IPointsAwardService pointsAwardService,
    IConfiguration configuration) : IWvWFightSummaryService
{
    private const int NameWidth = DiscordDisplayConstants.PlayerNameWidth;

    // Kept within DiscordTable.MaxRowWidth for Discord mobile embeds.
    internal static readonly DiscordTable.Column[] DamageColumns =
    [
        new("#", 2), new("Name", NameWidth),
        new("Damage", 6, DiscordTable.Align.Right), new("DownC", 6, DiscordTable.Align.Right)
    ];

    internal static readonly DiscordTable.Column[] CleanseColumns =
        [new("#", 2), new("Name", NameWidth), new("Cleanses", 8, DiscordTable.Align.Right)];

    internal static readonly DiscordTable.Column[] StabColumns =
    [
        new("#", 2), new("Name", NameWidth), new("Sub", 3, DiscordTable.Align.Right),
        new("S(on)", 5, DiscordTable.Align.Right), new("S(off)", 6, DiscordTable.Align.Right)
    ];

    internal static readonly DiscordTable.Column[] HealingColumns =
        [new("#", 2), new("Name", NameWidth), new("Healing", 7, DiscordTable.Align.Right)];

    internal static readonly DiscordTable.Column[] DistanceColumns =
        [new("#", 2), new("Name", NameWidth), new("Dist", 7, DiscordTable.Align.Right)];

    private static readonly DiscordTable.Column[] BarrierColumns =
        [new("#", 2), new("Name", NameWidth), new("Barrier", 7, DiscordTable.Align.Right)];

    private static readonly DiscordTable.Column[] TimesDownedColumns =
        [new("#", 2), new("Name", NameWidth), new("Downed", 6, DiscordTable.Align.Right)];

    private static readonly DiscordTable.Column[] StripColumns =
        [new("#", 2), new("Name", NameWidth), new("Strips", 6, DiscordTable.Align.Right)];

    internal static readonly DiscordTable.Column[] FriendlyColumns =
    [
        new("Who", 4), new("Count", 6),
        new("DMG", 6, DiscordTable.Align.Right), new("DPS", 6, DiscordTable.Align.Right),
        new("Downs", 5, DiscordTable.Align.Right), new("Deaths", 6, DiscordTable.Align.Right)
    ];

    // Keep the profession suffix visible when clipping the name.
    private static string DisplayName(string name, string? profession)
    {
        var append = EliteInsightExtensions.GetClassAppend(profession);
        return name.ClipAt(Math.Max(0, NameWidth - append.Length)) + append;
    }

    private static string AggregationBlock(string label, string ours, string theirs)
    {
        var columns = new DiscordTable.Column[]
        {
            new(label, Math.Max(label.Length, 6)),
            new("Ours", 8, DiscordTable.Align.Right),
            new("Theirs", 8, DiscordTable.Align.Right)
        };
        return $"```{DiscordTable.Header(columns)}{DiscordTable.Row(columns, string.Empty, ours, theirs)}```";
    }

    public async Task<(Embed Embed, string? WebAppUrl, long? FightLogId)> Generate(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
    {
        var playerCount = 5;

        var logLength = data.FightEliteInsightDataModel.Phases?.FirstOrDefault()?.EncounterDuration.TimeToSeconds() ?? 0;

        var friendlyCount = data.FightEliteInsightDataModel.Players?.Count ?? 0;
        var squadMemberCount = data.FightEliteInsightDataModel.Players?.Count(s => !s.NotInSquad) ?? 0;

        // Subtract one for the dummy PvP agent that ArcDPS always includes
        var enemyCount = (data.FightEliteInsightDataModel.Targets?.Count - 1) ?? 0;
        var enemyDamage = data.FightEliteInsightDataModel.Targets?
            .Sum(player => player.Details?.DmgDistributions?.Any() ?? false
                ? player.Details?.DmgDistributions[0].ContributedDamage
                : 0) ?? 0;

        var enemyDps = enemyDamage / logLength;

        var fightPhase = data.FightEliteInsightDataModel.Phases?.Any() ?? false
            ? data.FightEliteInsightDataModel.Phases[0]
            : new ArcDpsPhase();

        var gw2Players = playerService.GetGw2Players(data, fightPhase);

        var friendlyDamage = gw2Players.Sum(s => s.Damage);
        var friendlyDps = friendlyDamage / logLength;

        var friendlyCountStr = $"{friendlyCount}({squadMemberCount})".PadCenter(7);
        var friendlyDamageStr = friendlyDamage.FormatNumber().PadCenter(7);
        var friendlyDpsStr = friendlyDps.FormatNumber().PadCenter(7);
        var friendlyDownsStr = gw2Players.Sum(s => s.TimesDowned).ToString(CultureInfo.CurrentCulture).PadCenter(7);
        var friendlyDeathsStr = gw2Players.Sum(s => s.Deaths).ToString().PadCenter(7);

        var enemyCountStr = enemyCount.ToString().PadCenter(7);
        var enemyDamageStr = enemyDamage.FormatNumber().PadCenter(7);
        var enemyDpsStr = enemyDps.FormatNumber().PadCenter(7);
        var enemyDownsStr = gw2Players.Sum(s => s.Downs).ToString().PadCenter(7);
        var enemyDeathsStr = gw2Players.Sum(s => s.Kills).ToString().PadCenter(7);

        if (!advancedLog && guild.StreamLogChannelId.HasValue)
        {
            var streamMessage =
                $"```{DiscordTable.Header(FriendlyColumns)}" +
                DiscordTable.Row(FriendlyColumns, "Ally", friendlyCountStr.Trim(), friendlyDamageStr.Trim(), friendlyDpsStr.Trim(), friendlyDownsStr.Trim(), friendlyDeathsStr.Trim()) +
                DiscordTable.Row(FriendlyColumns, "Foe", enemyCountStr.Trim(), enemyDamageStr.Trim(), enemyDpsStr.Trim(), enemyDownsStr.Trim(), enemyDeathsStr.Trim()) +
                "```";

            if (client.GetChannel((ulong)guild.StreamLogChannelId) is ITextChannel streamLogChannel)
            {
                await streamLogChannel.SendMessageAsync(text: streamMessage);
            }
        }

        var range = (int)MathF.Min(15, data.FightEliteInsightDataModel.LogName?.Length - 1 ?? 0)..;
        var rangeStart = range.Start.GetOffset(data.FightEliteInsightDataModel.LogName?.Length ?? 0);
        var rangeEnd = range.End.GetOffset(data.FightEliteInsightDataModel.LogName?.Length ?? 0);

        if (rangeStart < 0 || rangeStart > data.FightEliteInsightDataModel.LogName?.Length || rangeEnd < 0 || rangeEnd > data.FightEliteInsightDataModel.LogName?.Length)
        {
            throw new Exception($"Bad battleground name: {data.FightEliteInsightDataModel.LogName}");
        }

        var battleGround = data.FightEliteInsightDataModel.LogName?[range] ?? string.Empty;

        var battleGroundEmoji = ":grey_question:";

        battleGroundEmoji = battleGround.Contains("Red", StringComparison.OrdinalIgnoreCase) ? ":red_square:" : battleGroundEmoji;
        battleGroundEmoji = battleGround.Contains("Blue", StringComparison.OrdinalIgnoreCase) ? ":blue_square:" : battleGroundEmoji;
        battleGroundEmoji = battleGround.Contains("Green", StringComparison.OrdinalIgnoreCase) ? ":green_square:" : battleGroundEmoji;
        battleGroundEmoji = battleGround.Contains("Eternal", StringComparison.OrdinalIgnoreCase) ? ":white_large_square:" : battleGroundEmoji;
        battleGroundEmoji = battleGround.Contains("Edge", StringComparison.OrdinalIgnoreCase) ? ":brown_square:" : battleGroundEmoji;

        var battleGroundColor = System.Drawing.Color.FromArgb(204, 214, 221);
        battleGroundColor = battleGround.Contains("Red", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(219, 44, 67) : battleGroundColor;
        battleGroundColor = battleGround.Contains("Blue", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(85, 172, 238) : battleGroundColor;
        battleGroundColor = battleGround.Contains("Green", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(123, 179, 91) : battleGroundColor;
        battleGroundColor = battleGround.Contains("Eternal", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(230, 231, 232) : battleGroundColor;
        battleGroundColor = battleGround.Contains("Edge", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Color.FromArgb(193, 105, 79) : battleGroundColor;

        var friendlyOverview = $"```{DiscordTable.Header(FriendlyColumns)}";
        friendlyOverview += DiscordTable.Row(FriendlyColumns, "Ally", friendlyCountStr.Trim(), friendlyDamageStr.Trim(), friendlyDpsStr.Trim(), friendlyDownsStr.Trim(), friendlyDeathsStr.Trim());
        friendlyOverview += DiscordTable.Row(FriendlyColumns, "Foe", enemyCountStr.Trim(), enemyDamageStr.Trim(), enemyDpsStr.Trim(), enemyDownsStr.Trim(), enemyDeathsStr.Trim());
        friendlyOverview += "```";

        var dateStartString = data.FightEliteInsightDataModel.Start;
        var dateTimeStart = DateTime.ParseExact(dateStartString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        var dateEndString = data.FightEliteInsightDataModel.End;
        var dateTimeEnd = DateTime.ParseExact(dateEndString, "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        var duration = dateTimeEnd - dateTimeStart;

        FightLog? fightLog = null;
        if (!advancedLog)
        {
            fightLog = await entityService.FightLog.GetFirstOrDefaultAsync(s => s.Url == data.FightEliteInsightDataModel.Url);

            if (fightLog == null)
            {
                await using var ctx = await dbContextFactory.CreateDbContextAsync();
                fightLog = await FightLogDeduplication.FindByContentAsync(
                    ctx, (short)FightTypesEnum.WvW, dateTimeStart,
                    gw2Players.Select(p => p.AccountName));
            }

            if (fightLog == null)
            {
                fightLog = new FightLog
                {
                    GuildId = guild.GuildId,
                    Url = data.FightEliteInsightDataModel.Url,
                    FightType = (short)FightTypesEnum.WvW,
                    FightStart = dateTimeStart,
                    FightDurationInMs = (long)duration.TotalMilliseconds,
                    IsSuccess = data.FightEliteInsightDataModel.Success ?? fightPhase.Success ?? false,
                    Source = FightLogHelpers.GetLogSource(data.FightEliteInsightDataModel.Url)
                };

                await entityService.FightLog.AddAsync(fightLog);

                var averageGroupDps = PlayerFightLogRoleClassifier.GetAverageGroupDps(gw2Players, fightLog.FightDurationInMs);
                var wvwBenchmarks = PlayerFightLogPlaystyleClassifier.BuildWvwBenchmarks(gw2Players, fightLog.FightDurationInMs);
                var playerFights = gw2Players.Select(gw2Player =>
                {
                    var boonRole = PlayerFightLogRoleClassifier.ResolveBoonRole(gw2Player, fightLog.FightDurationInMs, averageGroupDps);
                    return new PlayerFightLog
                    {
                        FightLogId = fightLog.FightLogId,
                        GuildWarsAccountName = gw2Player.AccountName,
                        CharacterName = gw2Player.CharacterName,
                        Damage = gw2Player.Damage,
                        Cleave = gw2Player.Cleave,
                        Kills = gw2Player.Kills,
                        Downs = gw2Player.Downs,
                        Deaths = gw2Player.Deaths,
                        QuicknessDuration = Convert.ToDecimal(gw2Player.TotalQuick),
                        AlacDuration = Convert.ToDecimal(gw2Player.TotalAlac),
                        QuicknessGenGroup = Convert.ToDecimal(gw2Player.QuicknessGenGroup),
                        AlacGenGroup = Convert.ToDecimal(gw2Player.AlacGenGroup),
                        BoonRole = boonRole,
                        Playstyle = PlayerFightLogPlaystyleClassifier.ResolveWvwPlaystyle(gw2Player, fightLog.FightDurationInMs, wvwBenchmarks),
                        SubGroup = gw2Player.SubGroup,
                        DamageDownContribution = gw2Player.DamageDownContribution,
                        Cleanses = Convert.ToInt64(gw2Player.Cleanses),
                        Strips = Convert.ToInt64(gw2Player.Strips),
                        StabGenOnGroup = Convert.ToDecimal(gw2Player.StabOnGroup),
                        StabGenOffGroup = Convert.ToDecimal(gw2Player.StabOffGroup),
                        Healing = gw2Player.Healing,
                        BarrierGenerated = gw2Player.BarrierGenerated,
                        DistanceFromTag = Convert.ToDecimal(gw2Player.DistanceFromTag),
                        TimesDowned = Convert.ToInt32(gw2Player.TimesDowned),
                        Interrupts = gw2Player.Interrupts,
                        NumberOfHitsWhileBlinded = gw2Player.NumberOfHitsWhileBlinded,
                        NumberOfMissesAgainst = Convert.ToInt64(gw2Player.NumberOfMissesAgainst),
                        NumberOfTimesBlockedAttack = Convert.ToInt64(gw2Player.NumberOfTimesBlockedAttack),
                        NumberOfTimesEnemyBlockedAttack = gw2Player.NumberOfTimesEnemyBlockedAttack,
                        NumberOfBoonsRipped = Convert.ToInt64(gw2Player.NumberOfBoonsRipped),
                        DamageTaken = Convert.ToInt64(gw2Player.DamageTaken),
                        BarrierMitigation = Convert.ToInt64(gw2Player.BarrierMitigation),
                        TimesInterrupted = gw2Player.TimesInterrupted,
                        ResurrectionTime = gw2Player.ResurrectionTime,
                        TimeOfDeath = gw2Player.TimeOfDeath,
                    };
                })
                    .ToList();

                await entityService.PlayerFightLog.AddRangeAsync(playerFights);
            }

            if (fightLog != null)
            {
                await FightLogHelpers.UpsertRawDataAsync(entityService, fightLog.FightLogId, data);
                await pointsAwardService.AwardFightAsync(fightLog.FightLogId);
            }
        }

        var webAppBaseUrl = configuration["WebApp:BaseUrl"];
        var webAppUrl = !advancedLog && !string.IsNullOrEmpty(webAppBaseUrl) && fightLog != null
            ? $"{webAppBaseUrl}/logs/{fightLog.FightLogId}"
            : null;

        var message = new EmbedBuilder
        {
            Title = $"{battleGroundEmoji} Report (WvW) - {battleGround}\n",
            Description = $"**Fight Duration:** {data.FightEliteInsightDataModel.Phases?.FirstOrDefault()?.EncounterDuration}\n",
            Color = (Color)battleGroundColor,
            Author = new EmbedAuthorBuilder()
            {
                Name = "GW2-DonBot",
                Url = "https://github.com/LoganWal/GW2-DonBot",
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Url = $"{data.FightEliteInsightDataModel.Url}"
        };

        message.AddField(x =>
        {
            x.Name = "Friendly Overview";
            x.Value = $"{friendlyOverview}";
            x.IsInline = false;
        });

        var embed = await GenerateMessage(advancedLog, playerCount, gw2Players, message, guild.GuildId);
        return (embed, webAppUrl, fightLog?.FightLogId);
    }

    public async Task<Embed> GenerateMessage(bool advancedLog, int playerCount, List<Gw2Player> gw2Players, EmbedBuilder message, long guildId, StatTotals? statTotals = null)
    {
        var damageOverview = $"```{DiscordTable.Header(DamageColumns)}";

        var maxDamage = -1.0f;
        var maxDownContribution = -1.0f;
        var topDamage = gw2Players.OrderByDescending(s => s.Damage).Take(playerCount).ToList();
        var damageIndex = 1;
        foreach (var gw2Player in topDamage)
        {
            var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
            var prof = gw2Player.Profession;
            var damageFloat = (float)gw2Player.Damage;
            if (maxDamage <= 0.0f)
            {
                maxDamage = damageFloat;
            }

            var downContribution = (float)gw2Player.DamageDownContribution;
            if (maxDownContribution <= 0.0f)
            {
                maxDownContribution = downContribution;
            }

            damageOverview += DiscordTable.Row(DamageColumns,
                damageIndex.ToString().PadLeft(2, '0'),
                DisplayName(name, prof),
                damageFloat.FormatNumber(maxDamage),
                downContribution.FormatNumber(maxDownContribution));
            damageIndex++;
        }

        damageOverview += "```";

        var cleanseOverview = $"```{DiscordTable.Header(CleanseColumns)}";

        var topCleanses = gw2Players.OrderByDescending(s => s.Cleanses).Take(playerCount).ToList();
        var cleanseIndex = 1;
        foreach (var gw2Player in topCleanses)
        {
            var cleanses = gw2Player.Cleanses;
            var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
            var prof = gw2Player.Profession;

            cleanseOverview += DiscordTable.Row(CleanseColumns,
                cleanseIndex.ToString().PadLeft(2, '0'),
                DisplayName(name, prof),
                cleanses.ToString(CultureInfo.InvariantCulture));
            cleanseIndex++;
        }

        cleanseOverview += "```";

        var stripOverview = $"```{DiscordTable.Header(StripColumns)}";

        var topStrips = gw2Players.OrderByDescending(s => s.Strips).Take(playerCount).ToList();
        var stripIndex = 1;
        foreach (var gw2Player in topStrips)
        {
            var strips = gw2Player.Strips;
            var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
            var prof = gw2Player.Profession;

            stripOverview += DiscordTable.Row(StripColumns,
                stripIndex.ToString().PadLeft(2, '0'),
                DisplayName(name, prof),
                strips.ToString(CultureInfo.InvariantCulture));
            stripIndex++;
        }

        stripOverview += "```";

        var stabOverview = $"```{DiscordTable.Header(StabColumns)}";

        var topStabs = gw2Players.OrderByDescending(s => s.StabOnGroup).Take(playerCount).ToList();
        var stabIndex = 1;
        foreach (var gw2Player in topStabs)
        {
            var stabOnGroup = gw2Player.StabOnGroup;
            var stabOffGroup = gw2Player.StabOffGroup;

            var sub = gw2Player.SubGroup;
            var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
            var prof = gw2Player.Profession;

            stabOverview += DiscordTable.Row(StabColumns,
                stabIndex.ToString().PadLeft(2, '0'),
                DisplayName(name, prof),
                sub.ToString(CultureInfo.InvariantCulture),
                Math.Round(stabOnGroup, 2).ToString(CultureInfo.InvariantCulture),
                Math.Round(stabOffGroup, 2).ToString(CultureInfo.InvariantCulture));
            stabIndex++;
        }

        stabOverview += "```";

        var healingOverview = $"```{DiscordTable.Header(HealingColumns)}";

        var topHealing = gw2Players.OrderByDescending(s => s.Healing).Take(playerCount).ToList();
        var healingIndex = 1;
        foreach (var gw2Player in topHealing)
        {
            var healing = gw2Player.Healing;
            var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
            var prof = gw2Player.Profession;

            healingOverview += DiscordTable.Row(HealingColumns,
                healingIndex.ToString().PadLeft(2, '0'),
                DisplayName(name, prof),
                healing.FormatNumber());
            healingIndex++;
        }

        healingOverview += "```";

        var distanceOverview = $"```{DiscordTable.Header(DistanceColumns)}";
        var timesDownedOverview = $"```{DiscordTable.Header(TimesDownedColumns)}";
        var barrierOverview = $"```{DiscordTable.Header(BarrierColumns)}";
        var aggregations = string.Empty;

        if (advancedLog)
        {
            var topBarrier = gw2Players.OrderByDescending(s => s.BarrierGenerated).Take(playerCount).ToList();
            var barrierIndex = 1;
            foreach (var gw2Player in topBarrier)
            {
                var barrier = gw2Player.BarrierGenerated;
                var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                var prof = gw2Player.Profession;

                barrierOverview += DiscordTable.Row(BarrierColumns,
                    barrierIndex.ToString().PadLeft(2, '0'),
                    DisplayName(name, prof),
                    barrier.FormatNumber());
                barrierIndex++;
            }
            barrierOverview += "```";

            var topDistance = gw2Players.OrderByDescending(s => s.DistanceFromTag).Take(playerCount).ToList();
            var distanceIndex = 1;
            foreach (var gw2Player in topDistance)
            {
                var distance = gw2Player.DistanceFromTag;
                var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                var prof = gw2Player.Profession;

                distanceOverview += DiscordTable.Row(DistanceColumns,
                    distanceIndex.ToString().PadLeft(2, '0'),
                    DisplayName(name, prof),
                    distance.ToString(CultureInfo.InvariantCulture));
                distanceIndex++;
            }
            distanceOverview += "```";

            var topTimesDowned = gw2Players.OrderByDescending(s => s.TimesDowned).Take(playerCount).ToList();
            var timesDownedIndex = 1;
            foreach (var gw2Player in topTimesDowned)
            {
                var timesDowned = gw2Player.TimesDowned;
                var name = !string.IsNullOrEmpty(gw2Player.CharacterName) ? gw2Player.CharacterName : gw2Player.AccountName;
                var prof = gw2Player.Profession;

                timesDownedOverview += DiscordTable.Row(TimesDownedColumns,
                    timesDownedIndex.ToString().PadLeft(2, '0'),
                    DisplayName(name, prof),
                    timesDowned.ToString(CultureInfo.InvariantCulture));
                timesDownedIndex++;
            }

            timesDownedOverview += "```";

            aggregations += AggregationBlock("Attacks Missed",
                gw2Players.Sum(s => s.NumberOfHitsWhileBlinded).ToString(CultureInfo.CurrentCulture),
                gw2Players.Sum(s => s.NumberOfMissesAgainst).ToString(CultureInfo.CurrentCulture));

            aggregations += AggregationBlock("Attacks Blocked",
                gw2Players.Sum(s => s.NumberOfTimesBlockedAttack).ToString(CultureInfo.CurrentCulture),
                gw2Players.Sum(s => s.NumberOfTimesEnemyBlockedAttack).ToString(CultureInfo.CurrentCulture));

            aggregations += AggregationBlock("Boons Stripped",
                (statTotals?.TotalStrips ?? gw2Players.Sum(s => s.Strips)).ToString(CultureInfo.CurrentCulture),
                gw2Players.Sum(s => s.NumberOfBoonsRipped).ToString(CultureInfo.CurrentCulture));

            var totalDmg = Convert.ToSingle(gw2Players.Sum(s => s.DamageTaken));
            var totalBarrierMitigation = Convert.ToSingle(gw2Players.Sum(s => s.BarrierMitigation));
            var diff = totalDmg - totalBarrierMitigation;
            var diffColumns = new DiscordTable.Column[]
            {
                new("Dmg Taken", 9, DiscordTable.Align.Right),
                new("Barrier", 8, DiscordTable.Align.Right),
                new("Diff", 16)
            };

            aggregations += $"```{DiscordTable.Header(diffColumns)}{DiscordTable.Row(diffColumns,
                totalDmg.FormatNumber(totalDmg),
                totalBarrierMitigation.FormatNumber(totalBarrierMitigation),
                $"{diff.FormatNumber(diff)}({Math.Round((totalBarrierMitigation / totalDmg) * 100, 2)}%)")}```";
        }

        if (!advancedLog)
        {
            message.AddField(x =>
            {
                x.Name = "Damage";
                x.Value = $"{damageOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Cleanses";
                x.Value = $"{cleanseOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Strips";
                x.Value = $"{stripOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Stab";
                x.Value = $"{stabOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Healing";
                x.Value = $"{healingOverview}";
                x.IsInline = false;
            });
        }

        if (advancedLog)
        {
            message.AddField(x =>
            {
                x.Name = "Barrier";
                x.Value = $"{barrierOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Times Downed";
                x.Value = $"{timesDownedOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Distance From Tag";
                x.Value = $"{distanceOverview}";
                x.IsInline = false;
            });

            message.AddField(x =>
            {
                x.Name = "Aggregations";
                x.Value = $"{aggregations}";
                x.IsInline = false;
            });
        }

        footerService.AddWidthSpacer(message);

        message.Footer = new EmbedFooterBuilder()
        {
            Text = $"{await footerService.Generate(guildId)}",
            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
        };

        message.Timestamp = DateTime.Now;

        return message.Build();
    }
}
