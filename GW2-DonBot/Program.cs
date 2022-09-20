using Discord;
using Discord.Webhook;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Testing.Models;
using static System.Net.WebRequestMethods;

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

// Battleground parsing
var battleGround = parsedData?.FightName.Substring(15);

var battleGroundEmoji = ":grey_question:";
battleGroundEmoji = battleGround.Contains("Red") ? ":red_square:" : battleGroundEmoji;
battleGroundEmoji = battleGround.Contains("Blue") ? ":blue_square:" : battleGroundEmoji;
battleGroundEmoji = battleGround.Contains("Green") ? ":green_square:" : battleGroundEmoji;
battleGroundEmoji = battleGround.Contains("Eternal") ? ":white_large_square:" : battleGroundEmoji;

var battleGroundColor = Color.Gray;
battleGroundColor = battleGround.Contains("Red") ? Color.FromArgb(219, 44, 67) : battleGroundColor;
battleGroundColor = battleGround.Contains("Blue") ? Color.FromArgb(85, 172, 238) : battleGroundColor;
battleGroundColor = battleGround.Contains("Green") ? Color.FromArgb(123, 179, 91) : battleGroundColor;
battleGroundColor = battleGround.Contains("Eternal") ? Color.FromArgb(230, 231, 232) : battleGroundColor;

// Embed content building
var friendlyOverview = "";
friendlyOverview    += $"```Players  Damage     DPS     Downs   Deaths \n";
friendlyOverview    +=    $"-------  -------  -------  -------  -------\n";
friendlyOverview    +=    $"{friendlyCountStr}  {friendlyDamageStr}  {friendlyDPSStr}  {friendlyDownsStr}  {friendlyDeathsStr}```";

var enemyOverview = "";
enemyOverview       += $"```Players  Damage     DPS     Downs   Deaths \n";
enemyOverview       +=    $"-------  -------  -------  -------  -------\n";
enemyOverview       +=    $"{enemyCountStr}  {enemyDamageStr}  {enemyDPSStr}  {enemyDownsStr}  {enemyDeathsStr}```";

var damageOverview = "";
damageOverview      += $"``` #           Name          Damage     DPS  \n";
damageOverview      +=    $"---  --------------------  -------  -------\n";
damageOverview      +=    $" 1   {parsedData?.Players[damageIndices[0]].Name.PadRight(20)}  {((float)damageValues[0]).FormatNumber((float)damageValues[0]).PadCenter(7)}  {((float)(damageValues[0] / logLength)).FormatNumber((float)(damageValues[0] / logLength)).PadCenter(7)}\n";
damageOverview      +=    $" 2   {parsedData?.Players[damageIndices[1]].Name.PadRight(20)}  {((float)damageValues[1]).FormatNumber((float)damageValues[0]).PadCenter(7)}  {((float)(damageValues[1] / logLength)).FormatNumber((float)(damageValues[0] / logLength)).PadCenter(7)}\n";
damageOverview      +=    $" 3   {parsedData?.Players[damageIndices[2]].Name.PadRight(20)}  {((float)damageValues[2]).FormatNumber((float)damageValues[0]).PadCenter(7)}  {((float)(damageValues[2] / logLength)).FormatNumber((float)(damageValues[0] / logLength)).PadCenter(7)}\n";
damageOverview      +=    $" 4   {parsedData?.Players[damageIndices[3]].Name.PadRight(20)}  {((float)damageValues[3]).FormatNumber((float)damageValues[0]).PadCenter(7)}  {((float)(damageValues[3] / logLength)).FormatNumber((float)(damageValues[0] / logLength)).PadCenter(7)}\n";
damageOverview      +=    $" 5   {parsedData?.Players[damageIndices[4]].Name.PadRight(20)}  {((float)damageValues[4]).FormatNumber((float)damageValues[0]).PadCenter(7)}  {((float)(damageValues[4] / logLength)).FormatNumber((float)(damageValues[0] / logLength)).PadCenter(7)}```";

var cleanseOverview = "";
cleanseOverview     += $"``` #           Name              Cleanses    \n";
cleanseOverview     +=    $"---  --------------------  ----------------\n";
cleanseOverview     +=    $" 1   {parsedData?.Players[cleanseIndices[0]].Name.PadRight(20)}  {cleanseValues[0].ToString().PadCenter(16)}\n";
cleanseOverview     +=    $" 2   {parsedData?.Players[cleanseIndices[1]].Name.PadRight(20)}  {cleanseValues[1].ToString().PadCenter(16)}\n";
cleanseOverview     +=    $" 3   {parsedData?.Players[cleanseIndices[2]].Name.PadRight(20)}  {cleanseValues[2].ToString().PadCenter(16)}\n";
cleanseOverview     +=    $" 4   {parsedData?.Players[cleanseIndices[3]].Name.PadRight(20)}  {cleanseValues[3].ToString().PadCenter(16)}\n";
cleanseOverview     +=    $" 5   {parsedData?.Players[cleanseIndices[4]].Name.PadRight(20)}  {cleanseValues[4].ToString().PadCenter(16)}```";

var stripOverview = "";
stripOverview       += $"``` #           Name               Strips     \n";
stripOverview       +=    $"---  --------------------  ----------------\n";
stripOverview       +=    $" 1   {parsedData?.Players[stripIndices[0]].Name.PadRight(20)}  {stripValues[0].ToString().PadCenter(16)}\n";
stripOverview       +=    $" 2   {parsedData?.Players[stripIndices[1]].Name.PadRight(20)}  {stripValues[1].ToString().PadCenter(16)}\n";
stripOverview       +=    $" 3   {parsedData?.Players[stripIndices[2]].Name.PadRight(20)}  {stripValues[2].ToString().PadCenter(16)}\n";
stripOverview       +=    $" 4   {parsedData?.Players[stripIndices[3]].Name.PadRight(20)}  {stripValues[3].ToString().PadCenter(16)}\n";
stripOverview       +=    $" 5   {parsedData?.Players[stripIndices[4]].Name.PadRight(20)}  {stripValues[4].ToString().PadCenter(16)}```";

var stabOverview = "";
stabOverview        += $"``` #           Name               Uptime     \n";
stabOverview        +=    $"---  --------------------  ----------------\n";
stabOverview        +=    $" 1   {parsedData?.Players[stabIndices[0]].Name.PadRight(20)}  {stabValues[0].FormatPercentage().PadCenter(16)}\n";
stabOverview        +=    $" 2   {parsedData?.Players[stabIndices[1]].Name.PadRight(20)}  {stabValues[1].FormatPercentage().PadCenter(16)}\n";
stabOverview        +=    $" 3   {parsedData?.Players[stabIndices[2]].Name.PadRight(20)}  {stabValues[2].FormatPercentage().PadCenter(16)}\n";
stabOverview        +=    $" 4   {parsedData?.Players[stabIndices[3]].Name.PadRight(20)}  {stabValues[3].FormatPercentage().PadCenter(16)}\n";
stabOverview        +=    $" 5   {parsedData?.Players[stabIndices[4]].Name.PadRight(20)}  {stabValues[4].FormatPercentage().PadCenter(16)}```";

// Prepping webhook
DiscordWebhook hook = new DiscordWebhook();
hook.Url = secrets.WebhookUrl;

// Building the message
DiscordMessage message = new DiscordMessage();
message.Content = ""; // keep this empty unless you want to ping someone? (this precedes the embed)"
message.TTS = false;
message.Username = "GW2-DonBot";
message.AvatarUrl = "https://i.imgur.com/tQ4LD6H.png";

// Embed
DiscordEmbed embed = new DiscordEmbed();
embed.Color = battleGroundColor;

embed.Author = new EmbedAuthor() { Name = "GW2-DonBot", Url = "https://github.com/LoganWal/GW2-DonBot", IconUrl = "https://i.imgur.com/tQ4LD6H.png" };

embed.Title = $"{battleGroundEmoji} Report (WvW) - {battleGround}\n";
embed.Url = $"{secrets.ScrapedUrl}";

embed.Description = $"**Fight Duration:** {parsedData?.EncounterDuration}\n";

//embed.Image = new EmbedMedia() { Url = "https://i.imgur.com/tQ4LD6H.png", Width = 10, Height = 10 }; //valid for thumb and video
//embed.Provider = new EmbedProvider() { Name = "Provider Name", Url = "Provider Url" };

// Actual embed content (populated via fields)
embed.Fields = new List<EmbedField>();
embed.Fields.Add(new EmbedField() { Name = "Friends",   Value = $"{friendlyOverview}",      InLine = false });
embed.Fields.Add(new EmbedField() { Name = "Enemies",   Value = $"{enemyOverview}",         InLine = false });
embed.Fields.Add(new EmbedField() { Name = "Damage",    Value = $"{damageOverview}",        InLine = false });
embed.Fields.Add(new EmbedField() { Name = "Cleanses",  Value = $"{cleanseOverview}",       InLine = false });
embed.Fields.Add(new EmbedField() { Name = "Strips",    Value = $"{stripOverview}",         InLine = false });
//embed.Fields.Add(new EmbedField() { Name = "Stability", Value = $"{stabOverview}",          InLine = false });

// Joke footers
var footerMessageVariants = new string[]
{
    "This bot brought to you by PoE, ty Chris!",
    "Did you know SoX is a PvE PoE guild?",
    "I'm not supposed to be on the internet...",
    "Just in: Squirrel is a murderer.",
    "Always be straight licking that shit!",
    "SHEEEEEEEEEEEEEEEEEEEE",
    "What do you like to tank on?",
    "Be the best dinker you can be!",
    "The fact you read this disgusts me.",
    "Alexa - make me a Discord bot."
};

var rng = new Random();
var footerVariantIndex = rng.Next(0, footerMessageVariants.Length);
embed.Footer = new EmbedFooter() { Text = $"{footerMessageVariants[footerVariantIndex]}", IconUrl = "https://i.imgur.com/tQ4LD6H.png" };
embed.Timestamp = DateTime.Now;

// Attaching embed
message.Embeds = new List<DiscordEmbed>();
message.Embeds.Add(embed);

// Sending the message
hook.Send(message);