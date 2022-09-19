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
var players = string.Join(Environment.NewLine, parsedData?.Players?.ToList()?.Select(player => $"{player?.Name}"));

// Prepping webhook
DiscordWebhook hook = new DiscordWebhook();
hook.Url = secrets.WebhookUrl;

// Building the message
DiscordMessage message = new DiscordMessage();
message.Content = $"ping <@{secrets.PingedUser}> \n```Latest players: \n{players}```";
message.TTS = false;
message.Username = "GW2-DonBot";
message.AvatarUrl = "https://i.imgur.com/tQ4LD6H.png";

// Sending the message
hook.Send(message);
