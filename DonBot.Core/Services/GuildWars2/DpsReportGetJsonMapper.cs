using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Models.GuildWars2.GetJson;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DonBot.Core.Services.GuildWars2;

public static class DpsReportGetJsonMapper
{
    public static EliteInsightDataModel Map(string json, string url)
    {
        var payload = JsonConvert.DeserializeObject<DpsReportGetJsonDataModel>(json) ??
            throw new InvalidOperationException("getJson response did not contain Elite Insights log data.");
        ValidateGetJsonPayload(payload);

        var fightData = MapGetJsonFightData(payload, url);
        var healingData = MapGetJsonHealingData(payload);
        var barrierData = MapGetJsonBarrierData(payload);

        // getJson is normalized into the legacy Elite Insights raw-data shape before storage.
        var normalizedRawFightData = JsonConvert.SerializeObject(fightData);
        var normalizedRawHealingData = healingData.HealingPhases.Count > 0
            ? JsonConvert.SerializeObject(healingData)
            : null;
        var normalizedRawBarrierData = barrierData.BarrierPhases.Count > 0
            ? JsonConvert.SerializeObject(barrierData)
            : null;

        return new EliteInsightDataModel(
            fightData,
            healingData,
            barrierData,
            normalizedRawFightData,
            normalizedRawHealingData,
            normalizedRawBarrierData);
    }

    private static void ValidateGetJsonPayload(DpsReportGetJsonDataModel payload)
    {
        if (payload.Error != null ||
            payload.Players == null ||
            payload.Targets == null ||
            payload.Phases == null)
        {
            throw new InvalidOperationException("getJson response did not contain Elite Insights log data.");
        }
    }

    private static FightEliteInsightDataModel MapGetJsonFightData(DpsReportGetJsonDataModel payload, string url)
    {
        var players = payload.Players ?? [];
        var targets = payload.Targets ?? [];
        var boons = MapBoons(payload, players);
        var fightMode = ResolveFightMode(payload);

        return new FightEliteInsightDataModel
        {
            Url = url,
            Boons = boons,
            Targets = targets.Select(MapTarget).ToList(),
            Players = players.Select(MapPlayer).ToList(),
            Phases = MapPhases(payload, players, targets.Count, payload.Mechanics, boons, fightMode),
            MechanicMap = MapMechanicMap(payload.Mechanics),
            SkillMap = MapSkillMap(payload.SkillMap),
            EncounterStart = payload.TimeStartStd ?? payload.EncounterStart ?? string.Empty,
            LogStart = payload.TimeStartStd ?? payload.LogStart ?? string.Empty,
            EncounterEnd = payload.TimeEndStd ?? payload.EncounterEnd ?? string.Empty,
            LogEnd = payload.TimeEndStd ?? payload.LogEnd ?? string.Empty,
            EncounterId = payload.EiEncounterId ?? payload.EncounterId,
            LogId = payload.EiLogId ?? payload.LogId ?? 0,
            Success = payload.Success,
            Wvw = payload.DetailedWvW == true || url.Contains("wvw.report", StringComparison.OrdinalIgnoreCase),
            LogName = payload.FightName ?? payload.LogName ?? payload.Name,
            FightMode = fightMode
        };
    }

    private static string ResolveFightMode(DpsReportGetJsonDataModel payload)
    {
        if (payload.IsLegendaryChallengeMode == true)
        {
            return "Legendary Challenge Mode";
        }

        return payload.IsChallengeMode == true
            ? "Challenge Mode"
            : "Normal Mode";
    }

    private static ArcDpsTarget MapTarget(GetJsonTarget target)
    {
        var totalHealth = target.TotalHealth ?? target.Health ?? 0;
        var finalHealth = target.FinalHealth ?? target.HpLeft ?? 0;

        return new ArcDpsTarget
        {
            HbWidth = target.HitboxWidth ?? target.HbWidth ?? 0,
            Percent = target.Percent ?? RemainingHealthPercent(totalHealth, finalHealth),
            HpLeft = finalHealth,
            Name = target.Name,
            Health = totalHealth,
            Details = MapTargetDetails(target)
        };
    }

    private static float RemainingHealthPercent(long totalHealth, long finalHealth)
    {
        if (totalHealth <= 0 || finalHealth < 0)
        {
            return 0f;
        }

        return (float)Math.Clamp(finalHealth / (double)totalHealth * 100d, 0d, 100d);
    }

    private static TargetDetails MapTargetDetails(GetJsonTarget target)
    {
        var allDamage = TokenAt(target.TotalDamageDist, 0) as JArray;
        if (allDamage == null)
        {
            return new TargetDetails();
        }

        var totalDamage = allDamage.Sum(entry => Value<long?>(entry, "totalDamage") ?? 0);
        var distribution = allDamage
            .Select(entry => new List<Distribution>
            {
                Value<bool?>(entry, "indirectDamage") ?? false,
                Value<double?>(entry, "id") ?? 0d,
                Value<double?>(entry, "totalDamage") ?? 0d
            })
            .ToList();

        return new TargetDetails
        {
            DmgDistributions =
            [
                new DmgDistribution
                {
                    ContributedDamage = totalDamage,
                    TotalDamage = totalDamage,
                    Distribution = distribution
                }
            ]
        };
    }

    private static ArcDpsPlayer MapPlayer(GetJsonPlayer player) => new()
    {
        Group = player.Group ?? 0,
        Acc = player.Account ?? player.Acc,
        Profession = player.Profession,
        NotInSquad = player.NotInSquad ?? false,
        Name = player.Name,
        Details = MapPlayerDetails(player)
    };

    private static ArcsDpsPlayerDetails MapPlayerDetails(GetJsonPlayer player) => new()
    {
        DeathRecap = MapDeathRecap(player),
        Rotation = MapRotation(player)
    };

    private static List<DeathRecap> MapDeathRecap(GetJsonPlayer player)
    {
        var deadRanges = player.CombatReplayData?["dead"] as JArray;
        if (deadRanges == null)
        {
            return [];
        }

        return deadRanges
            .Select(range => TokenAt(range, 0)?.Value<long?>())
            .Where(time => time.HasValue && time.Value >= 0)
            .Select(time => new DeathRecap { Time = time!.Value })
            .ToList();
    }

    private static List<List<List<double>>> MapRotation(GetJsonPlayer player)
    {
        if (player.Rotation == null)
        {
            return [];
        }

        var casts = new List<List<double>>();
        foreach (var skill in player.Rotation.Children<JObject>())
        {
            var skillId = Value<double?>(skill, "id") ?? 0d;
            foreach (var cast in (skill["skills"] as JArray)?.Children<JObject>() ?? [])
            {
                casts.Add(
                [
                    Value<double?>(cast, "castTime") ?? 0d,
                    skillId,
                    Value<double?>(cast, "duration") ?? 0d
                ]);
            }
        }

        return [casts.OrderBy(cast => cast[0]).ToList()];
    }

    private static List<ArcDpsPhase> MapPhases(
        DpsReportGetJsonDataModel payload,
        IReadOnlyList<GetJsonPlayer> players,
        int targetCount,
        IReadOnlyList<GetJsonMechanic>? mechanics,
        List<int> boons,
        string fightMode)
    {
        var phases = payload.Phases ?? [];
        return phases.Select((phase, phaseIndex) =>
        {
            var start = phase.Start ?? 0d;
            var end = phase.End ?? start;
            var durationMs = Math.Max(0d, end - start);

            return new ArcDpsPhase
            {
                Name = phase.Name ?? string.Empty,
                Start = start,
                End = end,
                Duration = Convert.ToInt64(Math.Round(durationMs)),
                Targets = MapPhaseTargets(phase),
                DpsStats = players.Select(player => MapDpsStats(TokenAt(player.DpsAll, phaseIndex))).ToList(),
                DpsStatsTargets = players.Select(player => MapTargetIndexedStats(player.DpsTargets, targetCount, phaseIndex, MapDpsStats)).ToList(),
                OffensiveStats = players.Select(player => MapOffensiveStats(TokenAt(player.StatsAll, phaseIndex))).ToList(),
                OffensiveStatsTargets = players.Select(player => MapTargetIndexedStats(player.StatsTargets, targetCount, phaseIndex, MapOffensiveStats)).ToList(),
                GameplayStats = players.Select(player => MapGameplayStats(TokenAt(player.StatsAll, phaseIndex))).ToList(),
                DefStats = players.Select(player => MapDefStats(TokenAt(player.Defenses, phaseIndex))).ToList(),
                SupportStats = players.Select(player => MapSupportStats(TokenAt(player.Support, phaseIndex))).ToList(),
                BuffsStatContainer = MapBuffsStatContainer(players, boons, phaseIndex),
                MechanicStats = MapMechanicStats(players, mechanics, start, end),
                Success = payload.Success,
                Mode = fightMode,
                EncounterDuration = TimeSpan.FromMilliseconds(durationMs).ToString(@"hh\:mm\:ss\.fff")
            };
        }).ToList();
    }

    private static List<int>? MapPhaseTargets(GetJsonPhase phase) => phase.Targets;

    private static List<List<long>> MapTargetIndexedStats(JToken? targetStats, int targetCount, int phaseIndex, Func<JToken?, List<long>> map)
    {
        var result = new List<List<long>>();
        for (var targetIndex = 0; targetIndex < targetCount; targetIndex++)
        {
            result.Add(map(TokenAt(TokenAt(targetStats, targetIndex), phaseIndex)));
        }

        return result;
    }

    private static List<long> MapDpsStats(JToken? token) =>
    [
        Value<long?>(token, "damage") ?? 0,
        Value<long?>(token, "dps") ?? 0,
        Value<long?>(token, "condiDamage") ?? 0,
        Value<long?>(token, "condiDps") ?? 0,
        Value<long?>(token, "powerDamage") ?? 0,
        Value<long?>(token, "powerDps") ?? 0,
        Value<long?>(token, "breakbarDamage") ?? 0
    ];

    private static List<long> MapOffensiveStats(JToken? token) =>
        LegacyArcDpsStatsBuilder.BuildOffensiveStats(
            Value<long?>(token, "missed") ?? 0,
            Value<long?>(token, "interrupts") ?? 0,
            Value<long?>(token, "blocked") ?? 0,
            Value<long?>(token, "killed") ?? 0,
            Value<long?>(token, "downed") ?? 0,
            Value<long?>(token, "downContribution") ?? 0);

    private static List<double> MapGameplayStats(JToken? token) =>
        LegacyArcDpsStatsBuilder.BuildGameplayStats(
            Value<double?>(token, "distToCom") ?? Value<double?>(token, "stackDist") ?? 0d);

    private static List<DefStat> MapDefStats(JToken? token) =>
        LegacyArcDpsStatsBuilder.BuildDefStats(
            Value<double?>(token, "damageTaken") ?? 0d,
            Value<double?>(token, "damageBarrier") ?? 0d,
            Value<double?>(token, "missedCount") ?? 0d,
            Value<double?>(token, "interruptedCount") ?? 0d,
            Value<double?>(token, "blockedCount") ?? 0d,
            Value<double?>(token, "boonStrips") ?? 0d,
            Value<double?>(token, "downCount") ?? 0d,
            Value<double?>(token, "deadCount") ?? 0d);

    private static List<double> MapSupportStats(JToken? token) =>
        LegacyArcDpsStatsBuilder.BuildSupportStats(
            Value<double?>(token, "condiCleanseSelf") ?? 0d,
            Value<double?>(token, "condiCleanseTimeSelf") ?? 0d,
            Value<double?>(token, "condiCleanse") ?? 0d,
            Value<double?>(token, "condiCleanseTime") ?? 0d,
            Value<double?>(token, "boonStrips") ?? 0d);

    private static BuffsStatContainer MapBuffsStatContainer(IReadOnlyList<GetJsonPlayer> players, List<int> boons, int phaseIndex) => new()
    {
        BoonStats = players.Select(player => new BoonActiveStat { Data = MapBuffData(player, boons, phaseIndex, "buffUptimesActive", "buffUptimes", "uptime") }).ToList(),
        BoonActiveStats = players.Select(player => new BoonActiveStat { Data = MapBuffData(player, boons, phaseIndex, "buffUptimesActive", "buffUptimes", "uptime") }).ToList(),
        BoonGenGroupStats = players.Select(player => new BoonActiveStat { Data = MapBuffData(player, boons, phaseIndex, "groupBuffsActive", "groupBuffs", "generation") }).ToList(),
        BoonGenOGroupStats = players.Select(player => new BoonActiveStat { Data = MapBuffData(player, boons, phaseIndex, "offGroupBuffsActive", "offGroupBuffs", "generation") }).ToList()
    };

    private static List<List<double>> MapBuffData(
        GetJsonPlayer player,
        List<int> boons,
        int phaseIndex,
        string primaryProperty,
        string fallbackProperty,
        string valueProperty)
    {
        var primary = BuffStatsFor(player, primaryProperty);
        var fallback = BuffStatsFor(player, fallbackProperty);
        return
        boons
            .Select(boonId => new List<double>
            {
                Value<double?>(TokenAt(FindBuff(primary, boonId)?["buffData"], phaseIndex), valueProperty)
                    ?? Value<double?>(TokenAt(FindBuff(fallback, boonId)?["buffData"], phaseIndex), valueProperty)
                    ?? 0d
            })
            .ToList();
    }

    private static JArray? BuffStatsFor(GetJsonPlayer player, string propertyName) =>
        propertyName switch
        {
            "buffUptimesActive" => player.BuffUptimesActive,
            "buffUptimes" => player.BuffUptimes,
            "groupBuffsActive" => player.GroupBuffsActive,
            "groupBuffs" => player.GroupBuffs,
            "offGroupBuffsActive" => player.OffGroupBuffsActive,
            "offGroupBuffs" => player.OffGroupBuffs,
            _ => null
        };

    private static JObject? FindBuff(JToken? buffs, int boonId) =>
        (buffs as JArray)?.Children<JObject>().FirstOrDefault(buff => Value<int?>(buff, "id") == boonId);

    private static List<List<object>>? MapMechanicStats(
        IReadOnlyList<GetJsonPlayer> players,
        IReadOnlyList<GetJsonMechanic>? mechanics,
        double phaseStart,
        double phaseEnd)
    {
        if (mechanics == null)
        {
            return null;
        }

        return players
            .Select(player => mechanics
                .Select(mechanic => (object)new JArray(CountMechanicHits(player, mechanic, phaseStart, phaseEnd)))
                .ToList())
            .ToList();
    }

    private static long CountMechanicHits(GetJsonPlayer player, GetJsonMechanic mechanic, double phaseStart, double phaseEnd)
    {
        return mechanic.MechanicsData?.Children<JObject>()
            .Where(hit =>
            {
                var time = Value<double?>(hit, "time") ?? -1d;
                if (time < phaseStart || time > phaseEnd)
                {
                    return false;
                }

                var actor = Value<string>(hit, "actor");
                var hitInstanceId = Value<long?>(hit, "instid");
                return (player.Name != null && string.Equals(actor, player.Name, StringComparison.Ordinal)) ||
                    (player.InstanceId.HasValue && hitInstanceId == player.InstanceId);
            })
            .Sum(hit => Value<long?>(hit, "weight") ?? 1L) ?? 0L;
    }

    private static List<MechanicMap>? MapMechanicMap(IReadOnlyList<GetJsonMechanic>? mechanics) =>
        mechanics?
            .Select(mechanic => new MechanicMap
            {
                Name = mechanic.Name,
                PlayerMech = true
            })
            .ToList();

    private static Dictionary<string, SkillMapEntry>? MapSkillMap(Dictionary<string, GetJsonSkillDefinition>? skillMap)
    {
        if (skillMap == null)
        {
            return null;
        }

        return skillMap.ToDictionary(
            property => property.Key,
            property => new SkillMapEntry
            {
                Id = ParsePrefixedId(property.Key),
                IsAutoAttack = property.Value.Aa ?? property.Value.AutoAttack ?? false,
                Name = property.Value.Name ?? string.Empty
            });
    }

    private static List<int> MapBoons(DpsReportGetJsonDataModel payload, IReadOnlyList<GetJsonPlayer> players)
    {
        var boons = payload.BuffMap?
            .Where(property => string.Equals(property.Value.Classification, "Boon", StringComparison.OrdinalIgnoreCase))
            .Select(property => Convert.ToInt32(ParsePrefixedId(property.Key)))
            .Where(id => id != 0)
            .ToList() ?? [];

        if (boons.Count > 0)
        {
            return boons;
        }

        return players
            .SelectMany(player => (player.BuffUptimesActive ?? [])
                .Concat(player.GroupBuffsActive ?? [])
                .Concat(player.OffGroupBuffsActive ?? []))
            .Select(buff => Value<int?>(buff, "id") ?? 0)
            .Where(id => id != 0)
            .Distinct()
            .ToList();
    }

    private static HealingEliteInsightDataModel MapGetJsonHealingData(DpsReportGetJsonDataModel payload)
    {
        var players = payload.Players ?? [];
        if (!players.Any(player => player.ExtHealingStats is not null))
        {
            return new HealingEliteInsightDataModel();
        }

        var phaseCount = payload.Phases?.Count ?? 0;
        var healingData = new HealingEliteInsightDataModel();
        for (var phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
        {
            var phase = new HealingPhase();
            foreach (var player in players)
            {
                var outgoing = ToInt(Value<long?>(TokenAt(player.ExtHealingStats?["outgoingHealing"], phaseIndex), "healing") ?? 0);
                var incoming = ToInt(Value<long?>(TokenAt(player.ExtHealingStats?["incomingHealing"], phaseIndex), "healing") ?? 0);
                var outgoingTargets = MapExtensionTargetStats(player.ExtHealingStats?["outgoingHealingAllies"], phaseIndex, "healing");
                phase.OutgoingHealingStats.Add([outgoing]);
                phase.OutgoingHealingStatsTargets.Add(outgoingTargets.Count > 0 ? outgoingTargets : [[outgoing]]);
                phase.IncomingHealingStats.Add([incoming]);
            }

            healingData.HealingPhases.Add(phase);
        }

        return healingData;
    }

    private static BarrierEliteInsightDataModel MapGetJsonBarrierData(DpsReportGetJsonDataModel payload)
    {
        var players = payload.Players ?? [];
        if (!players.Any(player => player.ExtBarrierStats is not null))
        {
            return new BarrierEliteInsightDataModel();
        }

        var phaseCount = payload.Phases?.Count ?? 0;
        var barrierData = new BarrierEliteInsightDataModel();
        for (var phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
        {
            var phase = new BarrierPhase();
            foreach (var player in players)
            {
                var outgoing = ToInt(Value<long?>(TokenAt(player.ExtBarrierStats?["outgoingBarrier"], phaseIndex), "barrier") ?? 0);
                var incoming = ToInt(Value<long?>(TokenAt(player.ExtBarrierStats?["incomingBarrier"], phaseIndex), "barrier") ?? 0);
                var outgoingTargets = MapExtensionTargetStats(player.ExtBarrierStats?["outgoingBarrierAllies"], phaseIndex, "barrier");
                phase.OutgoingBarrierStats.Add([outgoing]);
                phase.OutgoingBarrierStatsTargets.Add(outgoingTargets.Count > 0 ? outgoingTargets : [[outgoing]]);
                phase.IncomingBarrierStats.Add([incoming]);
            }

            barrierData.BarrierPhases.Add(phase);
        }

        return barrierData;
    }

    private static List<List<int>> MapExtensionTargetStats(JToken? targetStats, int phaseIndex, string valueProperty) =>
        (targetStats as JArray)?.Children()
            .Select(target => new List<int> { ToInt(Value<long?>(TokenAt(target, phaseIndex), valueProperty) ?? 0) })
            .ToList() ?? [];

    private static long ParsePrefixedId(string key)
    {
        var idText = key.TrimStart('b', 's', 'd', 't');
        return long.TryParse(idText, out var id) ? id : 0;
    }

    private static int ToInt(long value) =>
        Convert.ToInt32(Math.Clamp(value, int.MinValue, int.MaxValue));

    private static T? Value<T>(JToken? token, string propertyName)
    {
        if (token is not JObject obj)
        {
            return default;
        }

        return obj.TryGetValue(propertyName, out var value)
            ? value.Value<T>()
            : default;
    }

    private static JToken? TokenAt(JToken? token, int index) =>
        token is JArray array && index >= 0 && index < array.Count
            ? array[index]
            : null;
}
