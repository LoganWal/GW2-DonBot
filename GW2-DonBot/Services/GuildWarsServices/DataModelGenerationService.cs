using DonBot.Core.Models.GuildWars2;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DonBot.Services.GuildWarsServices;

public sealed class DataModelGenerationService(ILogger<DataModelGenerationService> logger, IHttpClientFactory httpClientFactory) : IDataModelGenerationService
{
    public EliteInsightDataModel GenerateEliteInsightDataModelFromHtml(string html, string url)
    {
        var logDataScript = ExtractLogDataScript(html);
        if (logDataScript == null)
        {
            logger.LogError("Failed to locate _logData in any <script> tag for {url}.", url);
            return new EliteInsightDataModel(url);
        }

        var fightData = ExtractAndDeserialize<FightEliteInsightDataModel>(logDataScript, "_logData");
        var healingData = ExtractAndDeserialize<HealingEliteInsightDataModel>(logDataScript, "_healingStatsExtension");
        var barrierData = ExtractAndDeserialize<BarrierEliteInsightDataModel>(logDataScript, "_barrierStatsExtension");

        var rawFightData = ExtractRaw(logDataScript, "_logData");
        var rawHealingData = ExtractRaw(logDataScript, "_healingStatsExtension");
        var rawBarrierData = ExtractRaw(logDataScript, "_barrierStatsExtension");

        fightData.Url = url;

        return new EliteInsightDataModel(fightData, healingData, barrierData, rawFightData, rawHealingData, rawBarrierData);
    }

    public EliteInsightDataModel GenerateEliteInsightDataModelFromJson(string json, string url)
    {
        try
        {
            return GenerateEliteInsightDataModelFromJsonCore(json, url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse getJson response for {url}.", url);
            return new EliteInsightDataModel(url);
        }
    }

    public async Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url)
    {
        const int maxRetries = 3;
        var attempt = 0;
        var reparseAttempted = false;
        var useGetJson = TryBuildGetJsonUrl(url, out var requestUrl, out var modelUrl);

        while (true)
        {
            try
            {
                using var response = await httpClientFactory.CreateClient().GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();
                using var content = response.Content;
                var result = await content.ReadAsStringAsync();

                if (useGetJson)
                {
                    return GenerateEliteInsightDataModelFromJsonCore(result, modelUrl);
                }

                var logDataScript = ExtractLogDataScript(result);
                if (logDataScript == null)
                {
                    logger.LogError("Failed to locate _logData in any <script> tag.");
                    throw new InvalidOperationException("_logData JSON object not found in the HTML.");
                }

                var fightData = ExtractAndDeserialize<FightEliteInsightDataModel>(logDataScript, "_logData");
                var healingData = ExtractAndDeserialize<HealingEliteInsightDataModel>(logDataScript, "_healingStatsExtension");
                var barrierData = ExtractAndDeserialize<BarrierEliteInsightDataModel>(logDataScript, "_barrierStatsExtension");

                var rawFightData = ExtractRaw(logDataScript, "_logData");
                var rawHealingData = ExtractRaw(logDataScript, "_healingStatsExtension");
                var rawBarrierData = ExtractRaw(logDataScript, "_barrierStatsExtension");

                fightData.Url = modelUrl;

                return new EliteInsightDataModel(fightData, healingData, barrierData, rawFightData, rawHealingData, rawBarrierData);
            }
            catch (Exception ex)
            {
                attempt++;

                if (!reparseAttempted && IsWingmanUrl(url))
                {
                    reparseAttempted = true;
                    logger.LogInformation("Wingman log not yet parsed, triggering reparse for {url}", url);
                    try
                    {
                        await TriggerWingmanReparseAsync(url);
                        logger.LogInformation("Reparse completed for {url}, retrying fetch", url);
                        attempt = 0;
                        continue;
                    }
                    catch (Exception reparseEx)
                    {
                        logger.LogWarning(reparseEx, "Wingman reparse failed for {url}", url);
                    }
                }

                logger.LogWarning(ex, "Attempt {attempt} failed to retrieve or process data from URL: {url}. Retrying in 1 second...", attempt, url);

                if (attempt >= maxRetries)
                {
                    logger.LogError("Max retries reached. Returning an empty EliteInsightDataModel.");
                    return new EliteInsightDataModel(modelUrl);
                }

                await Task.Delay(1000);
            }
        }
    }

    private static EliteInsightDataModel GenerateEliteInsightDataModelFromJsonCore(string json, string url)
    {
        var root = JObject.Parse(json);
        ValidateGetJsonPayload(root);

        var fightData = MapGetJsonFightData(root, url);
        var healingData = MapGetJsonHealingData(root);
        var barrierData = MapGetJsonBarrierData(root);

        var normalizedRawFightData = JsonConvert.SerializeObject(fightData);
        var normalizedRawHealingData = healingData.HealingPhases.Count > 0
            ? JsonConvert.SerializeObject(healingData)
            : null;
        var normalizedRawBarrierData = barrierData.BarrierPhases.Count > 0
            ? JsonConvert.SerializeObject(barrierData)
            : null;

        return new EliteInsightDataModel(fightData, healingData, barrierData, normalizedRawFightData, normalizedRawHealingData, normalizedRawBarrierData);
    }

    private static void ValidateGetJsonPayload(JObject root)
    {
        if (root["error"] != null ||
            root["players"] is not JArray ||
            root["targets"] is not JArray ||
            root["phases"] is not JArray)
        {
            throw new InvalidOperationException("getJson response did not contain Elite Insights log data.");
        }
    }

    private static bool TryBuildGetJsonUrl(string url, out string requestUrl, out string modelUrl)
    {
        requestUrl = url;
        modelUrl = url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        if (host is not ("dps.report" or "b.dps.report" or "wvw.report"))
        {
            return false;
        }

        var baseUrl = host == "wvw.report" ? "https://wvw.report" : "https://dps.report";
        if (uri.AbsolutePath.Equals("/getJson", StringComparison.OrdinalIgnoreCase))
        {
            var getJsonPermalink = GetQueryParameter(uri.Query, "permalink");
            if (!string.IsNullOrWhiteSpace(getJsonPermalink))
            {
                modelUrl = $"{baseUrl}/{getJsonPermalink}";
            }

            return true;
        }

        var permalink = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(permalink))
        {
            return false;
        }

        modelUrl = $"{baseUrl}/{permalink}";
        requestUrl = $"{baseUrl}/getJson?permalink={Uri.EscapeDataString(permalink)}";
        return true;
    }

    private static string? GetQueryParameter(string query, string name)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = part.IndexOf('=', StringComparison.Ordinal);
            var key = separatorIndex >= 0 ? part[..separatorIndex] : part;
            if (!string.Equals(Uri.UnescapeDataString(key), name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return separatorIndex >= 0 ? Uri.UnescapeDataString(part[(separatorIndex + 1)..]) : string.Empty;
        }

        return null;
    }

    private static FightEliteInsightDataModel MapGetJsonFightData(JObject root, string url)
    {
        var players = root["players"] as JArray ?? [];
        var targets = root["targets"] as JArray ?? [];
        var mechanics = root["mechanics"] as JArray;
        var boons = MapBoons(root, players);
        var fightMode = ResolveFightMode(root);

        return new FightEliteInsightDataModel
        {
            Url = url,
            Boons = boons,
            Targets = targets.Select(MapTarget).ToList(),
            Players = players.Select(MapPlayer).ToList(),
            Phases = MapPhases(root, players, targets.Count, mechanics, boons, fightMode),
            MechanicMap = MapMechanicMap(mechanics),
            SkillMap = MapSkillMap(root["skillMap"] as JObject),
            EncounterStart = Value<string>(root, "timeStartStd") ?? Value<string>(root, "encounterStart") ?? string.Empty,
            LogStart = Value<string>(root, "timeStartStd") ?? Value<string>(root, "logStart") ?? string.Empty,
            EncounterEnd = Value<string>(root, "timeEndStd") ?? Value<string>(root, "encounterEnd") ?? string.Empty,
            LogEnd = Value<string>(root, "timeEndStd") ?? Value<string>(root, "logEnd") ?? string.Empty,
            EncounterId = Value<long?>(root, "eiEncounterID") ?? Value<long?>(root, "encounterID"),
            LogId = Value<long?>(root, "eiLogID") ?? Value<long?>(root, "logID") ?? 0,
            Success = Value<bool?>(root, "success"),
            Wvw = Value<bool?>(root, "detailedWvW") == true || url.Contains("wvw.report", StringComparison.OrdinalIgnoreCase),
            LogName = Value<string>(root, "fightName") ?? Value<string>(root, "logName") ?? Value<string>(root, "name"),
            FightMode = fightMode
        };
    }

    private static string ResolveFightMode(JObject root)
    {
        if (Value<bool?>(root, "isLegendaryCM") == true)
        {
            return "Legendary Challenge Mode";
        }

        return Value<bool?>(root, "isCM") == true
            ? "Challenge Mode"
            : "Normal Mode";
    }

    private static ArcDpsTarget MapTarget(JToken token)
    {
        var totalHealth = Value<long?>(token, "totalHealth") ?? Value<long?>(token, "health") ?? 0;
        var finalHealth = Value<long?>(token, "finalHealth") ?? Value<long?>(token, "hpLeft") ?? 0;

        return new ArcDpsTarget
        {
            HbWidth = Value<int?>(token, "hitboxWidth") ?? Value<int?>(token, "hbWidth") ?? 0,
            Percent = Value<float?>(token, "percent") ?? RemainingHealthPercent(totalHealth, finalHealth),
            HpLeft = finalHealth,
            Name = Value<string>(token, "name"),
            Health = totalHealth,
            Details = MapTargetDetails(token)
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

    private static TargetDetails MapTargetDetails(JToken token)
    {
        var allDamage = TokenAt(token["totalDamageDist"], 0) as JArray;
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

    private static ArcDpsPlayer MapPlayer(JToken token) => new()
    {
        Group = Value<long?>(token, "group") ?? 0,
        Acc = Value<string>(token, "account") ?? Value<string>(token, "acc"),
        Profession = Value<string>(token, "profession"),
        NotInSquad = Value<bool?>(token, "notInSquad") ?? false,
        Name = Value<string>(token, "name"),
        Details = MapPlayerDetails(token)
    };

    private static ArcsDpsPlayerDetails MapPlayerDetails(JToken token) => new()
    {
        DeathRecap = MapDeathRecap(token),
        Rotation = MapRotation(token)
    };

    private static List<DeathRecap> MapDeathRecap(JToken token)
    {
        var deadRanges = token["combatReplayData"]?["dead"] as JArray;
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

    private static List<List<List<double>>> MapRotation(JToken token)
    {
        var rotation = token["rotation"] as JArray;
        if (rotation == null)
        {
            return [];
        }

        var casts = new List<List<double>>();
        foreach (var skill in rotation.Children<JObject>())
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
        JObject root,
        JArray players,
        int targetCount,
        JArray? mechanics,
        List<int> boons,
        string fightMode)
    {
        var phases = root["phases"] as JArray ?? [];
        return phases.Select((phase, phaseIndex) =>
        {
            var start = Value<double?>(phase, "start") ?? 0d;
            var end = Value<double?>(phase, "end") ?? start;
            var durationMs = Math.Max(0d, end - start);

            return new ArcDpsPhase
            {
                Name = Value<string>(phase, "name") ?? string.Empty,
                Start = start,
                End = end,
                Duration = Convert.ToInt64(Math.Round(durationMs)),
                Targets = MapPhaseTargets(phase),
                DpsStats = players.Select(player => MapDpsStats(TokenAt(player["dpsAll"], phaseIndex))).ToList(),
                DpsStatsTargets = players.Select(player => MapTargetIndexedStats(player["dpsTargets"], targetCount, phaseIndex, MapDpsStats)).ToList(),
                OffensiveStats = players.Select(player => MapOffensiveStats(TokenAt(player["statsAll"], phaseIndex))).ToList(),
                OffensiveStatsTargets = players.Select(player => MapTargetIndexedStats(player["statsTargets"], targetCount, phaseIndex, MapOffensiveStats)).ToList(),
                GameplayStats = players.Select(player => MapGameplayStats(TokenAt(player["statsAll"], phaseIndex))).ToList(),
                DefStats = players.Select(player => MapDefStats(TokenAt(player["defenses"], phaseIndex))).ToList(),
                SupportStats = players.Select(player => MapSupportStats(TokenAt(player["support"], phaseIndex))).ToList(),
                BuffsStatContainer = MapBuffsStatContainer(players, boons, phaseIndex),
                MechanicStats = MapMechanicStats(players, mechanics, start, end),
                Success = Value<bool?>(root, "success"),
                Mode = fightMode,
                EncounterDuration = TimeSpan.FromMilliseconds(durationMs).ToString(@"hh\:mm\:ss\.fff")
            };
        }).ToList();
    }

    private static List<int>? MapPhaseTargets(JToken phase) =>
        (phase["targets"] as JArray)?.Select(target => target.Value<int>()).ToList();

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

    private static List<long> MapOffensiveStats(JToken? token)
    {
        var stats = Enumerable.Repeat(0L, 18).ToList();
        stats[6] = Value<long?>(token, "missed") ?? 0;
        stats[7] = Value<long?>(token, "interrupts") ?? 0;
        stats[10] = Value<long?>(token, "blocked") ?? 0;
        stats[12] = Value<long?>(token, "killed") ?? 0;
        stats[13] = Value<long?>(token, "downed") ?? 0;
        stats[17] = Value<long?>(token, "downContribution") ?? 0;
        return stats;
    }

    private static List<double> MapGameplayStats(JToken? token)
    {
        var stats = Enumerable.Repeat(0d, 7).ToList();
        stats[6] = Value<double?>(token, "distToCom") ?? Value<double?>(token, "stackDist") ?? 0d;
        return stats;
    }

    private static List<DefStat> MapDefStats(JToken? token)
    {
        var stats = Enumerable.Range(0, 15).Select(_ => new DefStat { Double = 0d }).ToList();
        stats[0] = new DefStat { Double = Value<double?>(token, "damageTaken") ?? 0d };
        stats[1] = new DefStat { Double = Value<double?>(token, "damageBarrier") ?? 0d };
        stats[2] = new DefStat { Double = Value<double?>(token, "missedCount") ?? 0d };
        stats[3] = new DefStat { Double = Value<double?>(token, "interruptedCount") ?? 0d };
        stats[6] = new DefStat { Double = Value<double?>(token, "blockedCount") ?? 0d };
        stats[10] = new DefStat { Double = Value<double?>(token, "boonStrips") ?? 0d };
        stats[12] = new DefStat { Double = Value<double?>(token, "downCount") ?? 0d };
        stats[14] = new DefStat { Double = Value<double?>(token, "deadCount") ?? 0d };
        return stats;
    }

    private static List<double> MapSupportStats(JToken? token) =>
    [
        Value<double?>(token, "condiCleanseSelf") ?? 0d,
        Value<double?>(token, "condiCleanseTimeSelf") ?? 0d,
        Value<double?>(token, "condiCleanse") ?? 0d,
        Value<double?>(token, "condiCleanseTime") ?? 0d,
        Value<double?>(token, "boonStrips") ?? 0d
    ];

    private static BuffsStatContainer MapBuffsStatContainer(JArray players, List<int> boons, int phaseIndex) => new()
    {
        BoonStats = players.Select(player => new BoonActiveStat { Data = MapBuffData(player, boons, phaseIndex, "buffUptimesActive", "buffUptimes", "uptime") }).ToList(),
        BoonActiveStats = players.Select(player => new BoonActiveStat { Data = MapBuffData(player, boons, phaseIndex, "buffUptimesActive", "buffUptimes", "uptime") }).ToList(),
        BoonGenGroupStats = players.Select(player => new BoonActiveStat { Data = MapBuffData(player, boons, phaseIndex, "groupBuffsActive", "groupBuffs", "generation") }).ToList(),
        BoonGenOGroupStats = players.Select(player => new BoonActiveStat { Data = MapBuffData(player, boons, phaseIndex, "offGroupBuffsActive", "offGroupBuffs", "generation") }).ToList()
    };

    private static List<List<double>> MapBuffData(JToken player, List<int> boons, int phaseIndex, string primaryProperty, string fallbackProperty, string valueProperty) =>
        boons
            .Select(boonId => new List<double>
            {
                Value<double?>(TokenAt(FindBuff(player[primaryProperty], boonId)?["buffData"], phaseIndex), valueProperty)
                    ?? Value<double?>(TokenAt(FindBuff(player[fallbackProperty], boonId)?["buffData"], phaseIndex), valueProperty)
                    ?? 0d
            })
            .ToList();

    private static JObject? FindBuff(JToken? buffs, int boonId) =>
        (buffs as JArray)?.Children<JObject>().FirstOrDefault(buff => Value<int?>(buff, "id") == boonId);

    private static List<List<object>>? MapMechanicStats(JArray players, JArray? mechanics, double phaseStart, double phaseEnd)
    {
        if (mechanics == null)
        {
            return null;
        }

        return players.Children<JObject>()
            .Select(player => mechanics.Children<JObject>()
                .Select(mechanic => (object)new JArray(CountMechanicHits(player, mechanic, phaseStart, phaseEnd)))
                .ToList())
            .ToList();
    }

    private static long CountMechanicHits(JObject player, JObject mechanic, double phaseStart, double phaseEnd)
    {
        var name = Value<string>(player, "name");
        var instanceId = Value<long?>(player, "instanceID");

        return (mechanic["mechanicsData"] as JArray)?.Children<JObject>()
            .Where(hit =>
            {
                var time = Value<double?>(hit, "time") ?? -1d;
                if (time < phaseStart || time > phaseEnd)
                {
                    return false;
                }

                var actor = Value<string>(hit, "actor");
                var hitInstanceId = Value<long?>(hit, "instid");
                return (name != null && string.Equals(actor, name, StringComparison.Ordinal)) ||
                    (instanceId.HasValue && hitInstanceId == instanceId);
            })
            .Sum(hit => Value<long?>(hit, "weight") ?? 1L) ?? 0L;
    }

    private static List<MechanicMap>? MapMechanicMap(JArray? mechanics) =>
        mechanics?.Children<JObject>()
            .Select(mechanic => new MechanicMap
            {
                Name = Value<string>(mechanic, "name"),
                PlayerMech = true
            })
            .ToList();

    private static Dictionary<string, SkillMapEntry>? MapSkillMap(JObject? skillMap)
    {
        if (skillMap == null)
        {
            return null;
        }

        return skillMap.Properties().ToDictionary(
            property => property.Name,
            property => new SkillMapEntry
            {
                Id = ParsePrefixedId(property.Name),
                IsAutoAttack = Value<bool?>(property.Value, "aa") ?? Value<bool?>(property.Value, "autoAttack") ?? false,
                Name = Value<string>(property.Value, "name") ?? string.Empty
            });
    }

    private static List<int> MapBoons(JObject root, JArray players)
    {
        var boons = (root["buffMap"] as JObject)?.Properties()
            .Where(property => string.Equals(Value<string>(property.Value, "classification"), "Boon", StringComparison.OrdinalIgnoreCase))
            .Select(property => Convert.ToInt32(ParsePrefixedId(property.Name)))
            .Where(id => id != 0)
            .ToList() ?? [];

        if (boons.Count > 0)
        {
            return boons;
        }

        return players.Children<JObject>()
            .SelectMany(player => ((player["buffUptimesActive"] as JArray) ?? [])
                .Concat((player["groupBuffsActive"] as JArray) ?? [])
                .Concat((player["offGroupBuffsActive"] as JArray) ?? []))
            .Select(buff => Value<int?>(buff, "id") ?? 0)
            .Where(id => id != 0)
            .Distinct()
            .ToList();
    }

    private static HealingEliteInsightDataModel MapGetJsonHealingData(JObject root)
    {
        var players = root["players"] as JArray ?? [];
        if (!players.Any(player => player["extHealingStats"] is JObject))
        {
            return new HealingEliteInsightDataModel();
        }

        var phaseCount = (root["phases"] as JArray)?.Count ?? 0;
        var healingData = new HealingEliteInsightDataModel();
        for (var phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
        {
            var phase = new HealingPhase();
            foreach (var player in players)
            {
                var outgoing = ToInt(Value<long?>(TokenAt(player["extHealingStats"]?["outgoingHealing"], phaseIndex), "healing") ?? 0);
                var incoming = ToInt(Value<long?>(TokenAt(player["extHealingStats"]?["incomingHealing"], phaseIndex), "healing") ?? 0);
                var outgoingTargets = MapExtensionTargetStats(player["extHealingStats"]?["outgoingHealingAllies"], phaseIndex, "healing");
                phase.OutgoingHealingStats.Add([outgoing]);
                phase.OutgoingHealingStatsTargets.Add(outgoingTargets.Count > 0 ? outgoingTargets : [[outgoing]]);
                phase.IncomingHealingStats.Add([incoming]);
            }

            healingData.HealingPhases.Add(phase);
        }

        return healingData;
    }

    private static BarrierEliteInsightDataModel MapGetJsonBarrierData(JObject root)
    {
        var players = root["players"] as JArray ?? [];
        if (!players.Any(player => player["extBarrierStats"] is JObject))
        {
            return new BarrierEliteInsightDataModel();
        }

        var phaseCount = (root["phases"] as JArray)?.Count ?? 0;
        var barrierData = new BarrierEliteInsightDataModel();
        for (var phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
        {
            var phase = new BarrierPhase();
            foreach (var player in players)
            {
                var outgoing = ToInt(Value<long?>(TokenAt(player["extBarrierStats"]?["outgoingBarrier"], phaseIndex), "barrier") ?? 0);
                var incoming = ToInt(Value<long?>(TokenAt(player["extBarrierStats"]?["incomingBarrier"], phaseIndex), "barrier") ?? 0);
                var outgoingTargets = MapExtensionTargetStats(player["extBarrierStats"]?["outgoingBarrierAllies"], phaseIndex, "barrier");
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

    private string? ExtractLogDataScript(string html)
    {
        var scriptTags = new List<string>();
        var scriptStartIndex = 0;

        while ((scriptStartIndex = html.IndexOf("<script>", scriptStartIndex, StringComparison.Ordinal)) != -1)
        {
            var scriptEndIndex = html.IndexOf("</script>", scriptStartIndex, StringComparison.Ordinal);
            if (scriptEndIndex == -1)
            {
                logger.LogError("Malformed HTML: Missing closing </script> tag.");
                return null;
            }

            var scriptContent = html.Substring(scriptStartIndex + "<script>".Length, scriptEndIndex - scriptStartIndex - "<script>".Length);
            scriptTags.Add(scriptContent);
            scriptStartIndex = scriptEndIndex + "</script>".Length;
        }

        return scriptTags.FirstOrDefault(script => script.Contains("_logData = {"));
    }

    private static bool IsWingmanUrl(string url) =>
        url.Contains("gw2wingman.nevermindcreations.de/logContent/");

    private async Task TriggerWingmanReparseAsync(string logContentUrl)
    {
        var reparseUrl = logContentUrl.Replace("/logContent/", "/reparse/");
        logger.LogInformation("Calling wingman reparse endpoint: {ReparseUrl}", reparseUrl);

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        using var response = await client.GetAsync(reparseUrl);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<WingmanReparseResponse>(json);

        if (result?.Result != true)
        {
            throw new InvalidOperationException($"Wingman reparse did not return success for {reparseUrl}. Response: {json}");
        }
    }

    private class WingmanReparseResponse
    {
        [JsonProperty("result")]
        public bool Result { get; set; }
    }

    private string? ExtractRaw(string script, string variableName)
    {
        try
        {
            var startMarker = $"{variableName} = {{";
            var rawStartIndex = script.IndexOf(startMarker, StringComparison.Ordinal);
            if (rawStartIndex < 0)
            {
                return null;
            }

            var startIndex = rawStartIndex + startMarker.Length - 1;
            var endIndex = script.IndexOf("};", startIndex, StringComparison.Ordinal) + 1;

            if (endIndex <= 0)
            {
                return null;
            }

            return script.Substring(startIndex, endIndex - startIndex);
        }
        catch
        {
            return null;
        }
    }

    private T ExtractAndDeserialize<T>(string script, string variableName) where T : new()
    {
        try
        {
            var json = ExtractRaw(script, variableName);
            if (json == null)
            {
                logger.LogInformation("{variableName} not present in log - stats extension likely not enabled.", variableName);
                return new T();
            }

            return JsonConvert.DeserializeObject<T>(json) ?? new T();
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Failed to deserialize {variableName} JSON - stats extension likely not enabled.", variableName);
            return new T();
        }
    }
}
