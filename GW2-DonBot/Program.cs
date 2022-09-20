using Discord;
using Discord.Webhook;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Globalization;
using System.Linq;
using Testing.Models;

// Loading secrets
var secrets = new BotSecretsDataModel();
using (StreamReader r = new StreamReader("../../../Secrets/botSecrets.txt"))
{
    string json = r.ReadToEnd();
    secrets = JsonConvert.DeserializeObject<BotSecretsDataModel>(json);
}

// HTML scraping
var html = secrets.ScrapedUrl;
HtmlWeb web = new HtmlWeb();
var htmlDoc = web.Load(html);

// Registering start and end of actual log data inside the HTML
var logDataStartIndex = htmlDoc.ParsedText.IndexOf("logData") + 10;
var logDataEndIndex = htmlDoc.ParsedText.IndexOf("};");

var data = htmlDoc.ParsedText.Substring(logDataStartIndex, (logDataEndIndex - logDataStartIndex) + 1);

// Deserializing back to the data model
var parsedData = JsonConvert.DeserializeObject<EliteInsightDataModel>(data);

// -- Exploring the offensive stats breakdown
/*
var phase = parsedData?.Phases[1];
var players = parsedData?.Players;
string test2 = "";
for (int i = 0; i < players.Length; i++)
{
    test2 += $"{players[i].Name}:\n";

    //test2 += $"[{i}] Avg: {phase.BoonStats[i].Avg}\n";
    for (int x = 0; x < phase.OffensiveStats[i].Length; x++)
    {
        test2 += $"[{x}]: ";
        test2 += $"{phase.OffensiveStats[i][x]}";
        test2 += $"\n";
        //for (int y = 0; y < phase.OffensiveStats[i][x].Length; y++)
        //{
        //    test2 += $"[{x},{y}]: ";
        //    test2 += $"{phase.OffensiveStats[i][x][y]}";
        //    test2 += $"\n";
        //}
    }
}
*/

// Building the actual message to be sent
var content = "";
var playerNameList = string.Join(Environment.NewLine, parsedData?.Players?.ToList()?.Select(player => $"{player?.Name}"));
var logLength = parsedData?.EncounterDuration.TimeToSeconds();
var friendlyCount = parsedData?.Players?.Length;
var friendlyDamage = parsedData?.Players?.ToList()?.Sum(player => player.Details.DmgDistributions[0].ContributedDamage);
var friendlyDPS = (float)friendlyDamage / (float)logLength;
var enemyCount = parsedData?.Targets?.Length;
var enemyDamage = parsedData?.Targets?.ToList()?.Sum(player => player.Details.DmgDistributions[1].ContributedDamage);
var enemyDPS = (float)enemyDamage / (float)logLength;

var friendlyDowns = parsedData?.Phases[1].DefStats.ToList().Sum(playerDefStats => playerDefStats[8].Integer);
var friendlyDeaths = parsedData?.Phases[1].DefStats.ToList().Sum(playerDefStats => playerDefStats[10].Integer);

var enemyDowns = parsedData?.Phases[1].OffensiveStatsTargets.ToList().Sum(playerOffStats => playerOffStats[0][13]);
var enemyDeaths = parsedData?.Phases[1].OffensiveStatsTargets.ToList().Sum(playerOffStats => playerOffStats[0][12]);

var sortedByDamage = parsedData?.Phases[1].DpsStats.ToList().Select((playerStat, i) => new KeyValuePair<double, int>(playerStat[0], i)).OrderByDescending(playerStat => playerStat.Key).ToList();
var damageValues = sortedByDamage?.Select(x => x.Key).ToList();
var damageIndices = sortedByDamage?.Select(x => x.Value).ToList();

var sortedByCleanses = parsedData?.Phases[1].SupportStats.ToList().Select((playerStat, i) => new KeyValuePair<double, int>(playerStat[0] + playerStat[2], i)).OrderByDescending(playerStat => playerStat.Key).ToList();
var cleanseValues = sortedByCleanses?.Select(x => x.Key).ToList();
var cleanseIndices = sortedByCleanses?.Select(x => x.Value).ToList();

var sortedByStrips = parsedData?.Phases[1].SupportStats.ToList().Select((playerStat, i) => new KeyValuePair<double, int>(playerStat[4], i)).OrderByDescending(playerStat => playerStat.Key).ToList();
var stripValues = sortedByStrips?.Select(x => x.Key).ToList();
var stripIndices = sortedByStrips?.Select(x => x.Value).ToList();

var sortedByStab = parsedData?.Phases[1].BoonStats.ToList().Select((playerStat, i) => new KeyValuePair<double, int>(playerStat.Data[8] != null && playerStat.Data[8].Length >= 2 ? playerStat.Data[8][1] : 0.0, i)).OrderByDescending(playerSupStat => playerSupStat.Key).ToList();
var stabValues = sortedByStab?.Select(x => x.Key).ToList();
var stabIndices = sortedByStab?.Select(x => x.Value).ToList();

var friendlyCountStr = friendlyCount?.ToString().PadCenter(7);
var friendlyDamageStr = friendlyDamage?.FormatNumber().PadCenter(7);
var friendlyDPSStr = friendlyDPS.FormatNumber().PadCenter(7);
var friendlyDownsStr = friendlyDowns?.ToString().PadCenter(7);
var friendlyDeathsStr = friendlyDeaths?.ToString().PadCenter(7);

var enemyCountStr = enemyCount?.ToString().PadCenter(7);
var enemyDamageStr = enemyDamage?.FormatNumber().PadCenter(7);
var enemyDPSStr = enemyDPS.FormatNumber().PadCenter(7);
var enemyDownsStr = enemyDowns?.ToString().PadCenter(7);
var enemyDeathsStr = enemyDeaths?.ToString().PadCenter(7);

var battleGround = parsedData?.FightName.Substring(15);
var battleGroundEmoji = ":grey_question:";
battleGroundEmoji = battleGround.Contains("Red") ? ":red_circle:" : battleGroundEmoji;
battleGroundEmoji = battleGround.Contains("Blue") ? ":blue_circle:" : battleGroundEmoji;
battleGroundEmoji = battleGround.Contains("Green") ? ":green_circle:" : battleGroundEmoji;
battleGroundEmoji = battleGround.Contains("Eternal") ? ":white_circle:" : battleGroundEmoji;

content += $"-----------------------------------\n";
content += $"**Pings:** <@{secrets.PingedUser}>\n";
content += $"{battleGroundEmoji} **[WvW Report - {battleGround}]({secrets.ScrapedUrl})**\n";
content += $"**Fight Duration:** {parsedData?.EncounterDuration}\n";

content += $"**Friends:**";
content += $"```Players  Damage     DPS     Downs   Deaths \n";
content +=    $"-------  -------  -------  -------  -------\n";
content +=    $"{friendlyCountStr}  {friendlyDamageStr}  {friendlyDPSStr}  {friendlyDownsStr}  {friendlyDeathsStr}```";

content += $"**Enemies:**";
content += $"```Players  Damage     DPS     Downs   Deaths \n";
content +=    $"-------  -------  -------  -------  -------\n";
content +=    $"{enemyCountStr}  {enemyDamageStr}  {enemyDPSStr}  {enemyDownsStr}  {enemyDeathsStr}```";

content += $"**Damage:**";
content += $"``` #           Name          Damage      DPS\n";
content +=    $"---  -------------------  --------  --------\n";
content +=    $" 1   {parsedData?.Players[damageIndices[0]].Name.PadRight(19)}  {((float)damageValues[0]).FormatNumber().PadCenter(8)}  {((float)(damageValues[0] / logLength)).FormatNumber().PadCenter(8)}\n";
content +=    $" 2   {parsedData?.Players[damageIndices[1]].Name.PadRight(19)}  {((float)damageValues[1]).FormatNumber().PadCenter(8)}  {((float)(damageValues[1] / logLength)).FormatNumber().PadCenter(8)}\n";
content +=    $" 3   {parsedData?.Players[damageIndices[2]].Name.PadRight(19)}  {((float)damageValues[2]).FormatNumber().PadCenter(8)}  {((float)(damageValues[2] / logLength)).FormatNumber().PadCenter(8)}\n";
content +=    $" 4   {parsedData?.Players[damageIndices[3]].Name.PadRight(19)}  {((float)damageValues[3]).FormatNumber().PadCenter(8)}  {((float)(damageValues[3] / logLength)).FormatNumber().PadCenter(8)}\n";
content +=    $" 5   {parsedData?.Players[damageIndices[4]].Name.PadRight(19)}  {((float)damageValues[4]).FormatNumber().PadCenter(8)}  {((float)(damageValues[4] / logLength)).FormatNumber().PadCenter(8)}```";

content += $"**Cleanses:**";
content += $"``` #           Name         Cleanses\n";
content +=    $"---  -------------------  --------\n";
content +=    $" 1   {parsedData?.Players[cleanseIndices[0]].Name.PadRight(19)}  {cleanseValues[0].ToString().PadCenter(8)}\n";
content +=    $" 2   {parsedData?.Players[cleanseIndices[1]].Name.PadRight(19)}  {cleanseValues[1].ToString().PadCenter(8)}\n";
content +=    $" 3   {parsedData?.Players[cleanseIndices[2]].Name.PadRight(19)}  {cleanseValues[2].ToString().PadCenter(8)}\n";
content +=    $" 4   {parsedData?.Players[cleanseIndices[3]].Name.PadRight(19)}  {cleanseValues[3].ToString().PadCenter(8)}\n";
content +=    $" 5   {parsedData?.Players[cleanseIndices[4]].Name.PadRight(19)}  {cleanseValues[4].ToString().PadCenter(8)}```";

content += $"**Strips:**";
content += $"``` #           Name          Strips\n";
content +=    $"---  -------------------  --------\n";
content +=    $" 1   {parsedData?.Players[stripIndices[0]].Name.PadRight(19)}  {stripValues[0].ToString().PadCenter(8)}\n";
content +=    $" 2   {parsedData?.Players[stripIndices[1]].Name.PadRight(19)}  {stripValues[1].ToString().PadCenter(8)}\n";
content +=    $" 3   {parsedData?.Players[stripIndices[2]].Name.PadRight(19)}  {stripValues[2].ToString().PadCenter(8)}\n";
content +=    $" 4   {parsedData?.Players[stripIndices[3]].Name.PadRight(19)}  {stripValues[3].ToString().PadCenter(8)}\n";
content +=    $" 5   {parsedData?.Players[stripIndices[4]].Name.PadRight(19)}  {stripValues[4].ToString().PadCenter(8)}```";

/*
content += $"**Stability:**";
content += $"``` #           Name          Uptime\n";
content +=    $"---  -------------------  --------\n";
content +=    $" 1   {parsedData?.Players[stabIndices[0]].Name.PadRight(19)}  {stabValues[0].FormatPercentage().PadCenter(8)}\n";
content +=    $" 2   {parsedData?.Players[stabIndices[1]].Name.PadRight(19)}  {stabValues[1].FormatPercentage().PadCenter(8)}\n";
content +=    $" 3   {parsedData?.Players[stabIndices[2]].Name.PadRight(19)}  {stabValues[2].FormatPercentage().PadCenter(8)}\n";
content +=    $" 4   {parsedData?.Players[stabIndices[3]].Name.PadRight(19)}  {stabValues[3].FormatPercentage().PadCenter(8)}\n";
content +=    $" 5   {parsedData?.Players[stabIndices[4]].Name.PadRight(19)}  {stabValues[4].FormatPercentage().PadCenter(8)}```";
*/

/*
content += $"**Players Found:**";
content += $"```{playerNameList}```";
*/

content += $"-----------------------------------";

// Prepping webhook
DiscordWebhook hook = new DiscordWebhook();
hook.Url = secrets.WebhookUrl;

// Building the message
DiscordMessage message = new DiscordMessage();
message.Content = content;
message.TTS = false;
message.Username = "GW2-DonBot";
message.AvatarUrl = "https://i.imgur.com/tQ4LD6H.png";

// Sending the message
hook.Send(message);
