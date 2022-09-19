using Discord;
using Discord.Webhook;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Drawing;
using Testing.Models;

var html = @"";
HtmlWeb web = new HtmlWeb();
var htmlDoc = web.Load(html);
var logDataStartIndex = htmlDoc.ParsedText.IndexOf("logData") + 10;
var logDataEndIndex = htmlDoc.ParsedText.IndexOf("};");

var data = htmlDoc.ParsedText.Substring(logDataStartIndex, (logDataEndIndex - logDataStartIndex) + 1);

var parsedData = JsonConvert.DeserializeObject<EliteInsightDataModel>(data);

var players = string.Join(",", parsedData?.Players?.ToList()?.Select(player => $" {player?.Name}"));

var squirrel = false;

DiscordWebhook hook = new DiscordWebhook();
hook.Url = "";

DiscordMessage message = new DiscordMessage();
message.Content = $"Latest players {players}, ping <@225132265762586625>";
message.TTS = false;
message.Username = "Debuggin";
message.AvatarUrl = "";

//embeds
//DiscordEmbed embed = new DiscordEmbed();
//embed.Title = "Embed title";
//embed.Description = "Embed description";
//embed.Url = "Embed Url";
//embed.Timestamp = DateTime.Now;
//embed.Color = Color.Red; //alpha will be ignored, you can use any RGB color
//embed.Footer = new EmbedFooter() { Text = "Footer Text", IconUrl = "http://url-of-image" };
//embed.Image = new EmbedMedia() { Url = "Media URL", Width = 150, Height = 150 }; //valid for thumb and video
//embed.Provider = new EmbedProvider() { Name = "Provider Name", Url = "Provider Url" };
//embed.Author = new EmbedAuthor() { Name = "Author Name", Url = "Author Url", IconUrl = "http://url-of-image" };

//fields
//embed.Fields = new List<EmbedField>();
//embed.Fields.Add(new EmbedField() { Name = "Field Name", Value = "Field Value", InLine = true });
//embed.Fields.Add(new EmbedField() { Name = "Field Name 2", Value = "Field Value 2", InLine = true });

//set embed
//message.Embeds = new List<DiscordEmbed>();
//message.Embeds.Add(embed);

//message
hook.Send(message);