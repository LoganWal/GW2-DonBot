using Discord;
using Discord.Webhook;
using HtmlAgilityPack;
using Newtonsoft.Json;
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

// Building the actual message to be sent
var content = "";
var playerNameList = string.Join(Environment.NewLine, parsedData?.Players?.ToList()?.Select(player => $"{player?.Name}"));
var friendlyCount = parsedData?.Players?.Length;
var friendlyDamage = parsedData?.Players?.ToList()?.Sum(player => player.Details.DmgDistributions[0].ContributedDamage);
var friendlyDPS = (float)friendlyDamage / (float)friendlyCount;
var enemyCount = parsedData?.Targets?.Length;
var enemyDamage = parsedData?.Targets?.ToList()?.Sum(player => player.Details.DmgDistributions[1].ContributedDamage);
var enemyDPS = (float)enemyDamage / (float)enemyCount;

/* -- Exploring the offensive stats breakdown
var phase = parsedData?.Phases[1];
var players = parsedData?.Players;
string test2 = "";
for (int i = 0; i < players.Length; i++)
{
    test2 += $"{players[i].Name}:\n";
    
    for (int x = 0; x < phase.OffensiveStatsTargets[i].Length; x++)
    {
        for (int y = 0; y < phase.OffensiveStatsTargets[i][x].Length; y++)
        {
            test2 += $"[{x},{y}]: ";
            test2 += $"{phase.OffensiveStatsTargets[i][x][y]}";
            test2 += $"\n";
        }
    }
}
*/

var friendlyDowns = parsedData?.Phases[1].DefStats.ToList().Sum(playerDefStats => playerDefStats[8].Integer);
var friendlyDeaths = parsedData?.Phases[1].DefStats.ToList().Sum(playerDefStats => playerDefStats[10].Integer);

var enemyDowns = parsedData?.Phases[1].OffensiveStatsTargets.ToList().Sum(playerOffStats => playerOffStats[0][13]);
var enemyDeaths = parsedData?.Phases[1].OffensiveStatsTargets.ToList().Sum(playerOffStats => playerOffStats[0][12]);

var friendlyCountStr = friendlyCount?.ToString().PadCenter(7);
var friendlyDamageStr = friendlyDamage?.ToString().PadCenter(7);
var friendlyDPSStr = friendlyDPS.ToString("F0").PadCenter(7);
var friendlyDownsStr = friendlyDowns?.ToString().PadCenter(7);
var friendlyDeathsStr = friendlyDeaths?.ToString().PadCenter(7);

var enemyCountStr = enemyCount?.ToString().PadCenter(7);
var enemyDamageStr = enemyDamage?.ToString().PadCenter(7);
var enemyDPSStr = enemyDPS.ToString("F0").PadCenter(7);
var enemyDownsStr = enemyDowns?.ToString().PadCenter(7);
var enemyDeathsStr = enemyDeaths?.ToString().PadCenter(7);

content += $"Have a look at my new report! <@{secrets.PingedUser}>\n";
content += $"**Friends:**";
content += $"```Players  Damage     DPS     Downs   Deaths \n";
content +=    $"-------  -------  -------  -------  -------\n";
content +=    $"{friendlyCountStr}  {friendlyDamageStr}  {friendlyDPSStr}  {friendlyDownsStr}  {friendlyDeathsStr}```";
content += $"**Enemies:**";
content += $"```Players  Damage     DPS     Downs   Deaths \n";
content +=    $"-------  -------  -------  -------  -------\n";
content +=    $"{enemyCountStr}  {enemyDamageStr}  {enemyDPSStr}  {enemyDownsStr}  {enemyDeathsStr}```";
content += $"**Players Found:**";
content += $"```{playerNameList}```";

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
