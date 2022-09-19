using Discord;
using Discord.Webhook;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Testing.Models;

var html = @"";
HtmlWeb web = new HtmlWeb();
var htmlDoc = web.Load(html);
var logDataStartIndex = htmlDoc.ParsedText.IndexOf("logData") + 10;
var logDataEndIndex = htmlDoc.ParsedText.IndexOf("};");

var data = htmlDoc.ParsedText.Substring(logDataStartIndex, (logDataEndIndex - logDataStartIndex) + 1);

var parsedData = JsonConvert.DeserializeObject<EliteInsightDataModel>(data);

var players = string.Join(Environment.NewLine, parsedData?.Players?.ToList()?.Select(player => $"{player?.Name}"));

DiscordWebhook hook = new DiscordWebhook();
hook.Url = "";

DiscordMessage message = new DiscordMessage();
message.Content = $"ping <@225132265762586625> \n```Latest players: \n{players}```";
message.TTS = false;
message.Username = "GW2-DonBot";
message.AvatarUrl = "https://i.imgur.com/tQ4LD6H.png";

hook.Send(message);
